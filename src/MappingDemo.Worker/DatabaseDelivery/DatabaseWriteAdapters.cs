using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using MappingDemo.Shared.DatabaseDestinations;
using MappingDemo.Shared.Delivery;
using MappingDemo.Shared.Destinations;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace MappingDemo.Worker.DatabaseDelivery;

public sealed record DatabaseWriteRequest(
    string ProfileId,
    string ProfileConfigurationHash,
    TargetSchemaSnapshot SchemaSnapshot,
    DatabaseDuplicatePolicy DuplicatePolicy,
    IReadOnlyDictionary<string, JsonElement> Values);

public sealed record DatabaseWriteResult(
    string Status,
    string? ErrorCode = null,
    string? ErrorRedacted = null,
    string? CurrentSchemaHash = null);

public interface IDatabaseWriteAdapter
{
    DatabaseProvider Provider { get; }

    Task<DatabaseWriteResult> WriteAsync(
        DatabaseWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DatabaseWriteModule
{
    private readonly IReadOnlyDictionary<DatabaseProvider, IDatabaseWriteAdapter>
        _adapters;

    public DatabaseWriteModule(IEnumerable<IDatabaseWriteAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(adapter => adapter.Provider);
        foreach (var provider in Enum.GetValues<DatabaseProvider>())
        {
            if (!_adapters.ContainsKey(provider))
            {
                throw new InvalidOperationException(
                    $"Database write adapter for '{provider}' is missing.");
            }
        }
    }

    public Task<DatabaseWriteResult> WriteAsync(
        DatabaseWriteRequest request,
        CancellationToken cancellationToken = default) =>
        _adapters[request.SchemaSnapshot.Provider].WriteAsync(
            request,
            cancellationToken);
}

public sealed class PostgreSqlDatabaseWriteAdapter(
    IDatabaseDestinationProfileCatalog profiles,
    IDatabaseDestinationConnectionFactory connections,
    IDatabaseTargetSchemaInspector inspector)
    : RelationalDatabaseWriteAdapter(profiles, connections, inspector)
{
    public override DatabaseProvider Provider => DatabaseProvider.PostgreSql;

    protected override string Quote(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    protected override ProviderFailure Classify(DbException exception) =>
        exception is PostgresException postgres
            ? postgres.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation => ProviderFailure.Unique,
                PostgresErrorCodes.NotNullViolation or
                    PostgresErrorCodes.ForeignKeyViolation or
                    PostgresErrorCodes.CheckViolation or
                    PostgresErrorCodes.InvalidTextRepresentation or
                    PostgresErrorCodes.NumericValueOutOfRange =>
                    ProviderFailure.Permanent,
                PostgresErrorCodes.UndefinedTable or
                    PostgresErrorCodes.UndefinedColumn =>
                    ProviderFailure.Schema,
                PostgresErrorCodes.SerializationFailure or
                    PostgresErrorCodes.DeadlockDetected or
                    PostgresErrorCodes.ConnectionException or
                    PostgresErrorCodes.ConnectionFailure or
                    PostgresErrorCodes.SqlClientUnableToEstablishSqlConnection =>
                    ProviderFailure.Transient,
                _ when postgres.IsTransient => ProviderFailure.Transient,
                _ => ProviderFailure.Permanent
            }
            : ProviderFailure.Transient;
}

public sealed class SqlServerDatabaseWriteAdapter(
    IDatabaseDestinationProfileCatalog profiles,
    IDatabaseDestinationConnectionFactory connections,
    IDatabaseTargetSchemaInspector inspector)
    : RelationalDatabaseWriteAdapter(profiles, connections, inspector)
{
    public override DatabaseProvider Provider => DatabaseProvider.SqlServer;

    protected override string Quote(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    protected override ProviderFailure Classify(DbException exception)
    {
        if (exception is not SqlException sql)
        {
            return ProviderFailure.Transient;
        }

        return sql.Number switch
        {
            2601 or 2627 => ProviderFailure.Unique,
            207 or 208 => ProviderFailure.Schema,
            245 or 515 or 547 or 8115 or 2628 => ProviderFailure.Permanent,
            -2 or 20 or 53 or 64 or 233 or 1205 or 4060 or 10053 or
                10054 or 10060 or 10928 or 10929 or 40197 or 40501 or
                40613 => ProviderFailure.Transient,
            _ => ProviderFailure.Permanent
        };
    }
}

public abstract class RelationalDatabaseWriteAdapter(
    IDatabaseDestinationProfileCatalog profiles,
    IDatabaseDestinationConnectionFactory connections,
    IDatabaseTargetSchemaInspector inspector) : IDatabaseWriteAdapter
{
    public abstract DatabaseProvider Provider { get; }

    protected abstract string Quote(string identifier);

    protected abstract ProviderFailure Classify(DbException exception);

    public async Task<DatabaseWriteResult> WriteAsync(
        DatabaseWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = profiles.GetRequired(request.ProfileId);
        if (profile.Provider != Provider ||
            !string.Equals(
                profile.ConfigurationHash,
                request.ProfileConfigurationHash,
                StringComparison.Ordinal))
        {
            return Failure(
                DatabaseDeliveryJobStatus.BlockedSchemaDrift,
                "ProfileChanged",
                "Database destination profile changed after job creation.");
        }
        if (!profile.WriteEnabled)
        {
            return Failure(
                DatabaseDeliveryJobStatus.Pending,
                "WriteDisabled",
                "Database destination writes are disabled.");
        }

        TargetTableSchema current;
        try
        {
            current = await inspector.InspectAsync(
                new TargetTableReference(
                    request.ProfileId,
                    request.SchemaSnapshot.Schema,
                    request.SchemaSnapshot.Table),
                refresh: true,
                cancellationToken);
        }
        catch (DatabaseSchemaInspectionException)
        {
            return Failure(
                DatabaseDeliveryJobStatus.RetryableFailed,
                "SchemaUnavailable",
                "Target schema inspection is unavailable.");
        }

        var keyColumns = request.DuplicatePolicy is
            ExistingUniqueKeyDuplicatePolicy key
                ? key.Columns
                : [];
        var compatibility = TargetSchemaCompatibilityAnalyzer.Compare(
            request.SchemaSnapshot,
            current,
            request.Values.Keys.ToHashSet(StringComparer.Ordinal),
            keyColumns);
        if (compatibility.BlocksDelivery)
        {
            return new DatabaseWriteResult(
                DatabaseDeliveryJobStatus.BlockedSchemaDrift,
                "BreakingSchemaDrift",
                "Target schema has a breaking change.",
                current.SchemaHash);
        }

        PreparedWrite prepared;
        try
        {
            prepared = Prepare(request);
        }
        catch (DatabaseWriteValidationException exception)
        {
            return Failure(
                DatabaseDeliveryJobStatus.PermanentFailed,
                exception.Code,
                exception.Message);
        }

        await using var connection = connections.CreateConnection(
            request.ProfileId);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbException)
        {
            return Failure(
                DatabaseDeliveryJobStatus.RetryableFailed,
                "ConnectionUnavailable",
                "Target database connection is unavailable.");
        }

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var commitStarted = false;
        try
        {
            await ExecuteInsertAsync(
                connection,
                transaction,
                prepared,
                profile.CommandTimeoutSeconds,
                cancellationToken);
            commitStarted = true;
            await transaction.CommitAsync(cancellationToken);
            return new DatabaseWriteResult(
                DatabaseDeliveryJobStatus.Delivered,
                CurrentSchemaHash: current.SchemaHash);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbException exception)
        {
            if (!commitStarted)
            {
                await SafeRollbackAsync(transaction);
                var failure = Classify(exception);
                if (failure == ProviderFailure.Unique)
                {
                    return await ReconcileAsync(
                        request,
                        profile.CommandTimeoutSeconds,
                        ambiguous: false,
                        cancellationToken);
                }
                return failure switch
                {
                    ProviderFailure.Schema => Failure(
                        DatabaseDeliveryJobStatus.BlockedSchemaDrift,
                        "SchemaCommandRejected",
                        "Target rejected the command because its schema changed."),
                    ProviderFailure.Permanent => Failure(
                        DatabaseDeliveryJobStatus.PermanentFailed,
                        "ConstraintOrTypeRejected",
                        "Target rejected a constraint or typed value."),
                    _ => Failure(
                        DatabaseDeliveryJobStatus.RetryableFailed,
                        "TransientTargetFailure",
                        "Target database reported a transient failure.")
                };
            }

            return await ReconcileAsync(
                request,
                profile.CommandTimeoutSeconds,
                ambiguous: true,
                cancellationToken);
        }
        catch (TimeoutException)
        {
            return commitStarted
                ? await ReconcileAsync(
                    request,
                    profile.CommandTimeoutSeconds,
                    ambiguous: true,
                    cancellationToken)
                : Failure(
                    DatabaseDeliveryJobStatus.RetryableFailed,
                    "TargetTimeout",
                    "Target database timed out before commit.");
        }
    }

    private PreparedWrite Prepare(DatabaseWriteRequest request)
    {
        var columns = request.SchemaSnapshot.Columns.ToDictionary(
            column => column.Name,
            StringComparer.Ordinal);
        var values = new List<PreparedValue>();
        foreach (var pair in request.Values.OrderBy(
                     pair => columns.TryGetValue(pair.Key, out var column)
                         ? column.Ordinal
                         : int.MaxValue))
        {
            if (!columns.TryGetValue(pair.Key, out var column) ||
                !column.IsWritable ||
                column.ScalarType == DatabaseScalarType.Unsupported)
            {
                throw new DatabaseWriteValidationException(
                    "InvalidMappedColumn",
                    "Mapped target column is unavailable or not writable.");
            }
            values.Add(new PreparedValue(
                column,
                ConvertValue(pair.Value, column)));
        }

        var missing = request.SchemaSnapshot.Columns.FirstOrDefault(column =>
            column.IsRequired && !request.Values.ContainsKey(column.Name));
        if (missing is not null)
        {
            throw new DatabaseWriteValidationException(
                "RequiredValueMissing",
                "A required target value is missing.");
        }

        var table = $"{Quote(request.SchemaSnapshot.Schema)}.{Quote(request.SchemaSnapshot.Table)}";
        var sql = values.Count == 0
            ? $"INSERT INTO {table} DEFAULT VALUES"
            : $"INSERT INTO {table} ({string.Join(", ", values.Select(value => Quote(value.Column.Name)))}) VALUES ({string.Join(", ", values.Select((_, index) => $"@p{index}"))})";
        return new PreparedWrite(sql, values);
    }

    private async Task ExecuteInsertAsync(
        DbConnection connection,
        DbTransaction transaction,
        PreparedWrite prepared,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = prepared.Sql;
        command.CommandTimeout = timeoutSeconds;
        for (var index = 0; index < prepared.Values.Count; index++)
        {
            AddParameter(command, $"@p{index}", prepared.Values[index]);
        }
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<DatabaseWriteResult> ReconcileAsync(
        DatabaseWriteRequest request,
        int timeoutSeconds,
        bool ambiguous,
        CancellationToken cancellationToken)
    {
        if (request.DuplicatePolicy is not ExistingUniqueKeyDuplicatePolicy key)
        {
            return Failure(
                DatabaseDeliveryJobStatus.UnknownOutcome,
                ambiguous ? "AmbiguousCommit" : "UniqueKeyUnavailable",
                "Delivery outcome cannot be proven without a usable unique key.");
        }

        try
        {
            var columns = request.SchemaSnapshot.Columns.ToDictionary(
                column => column.Name,
                StringComparer.Ordinal);
            if (key.Columns.Any(column => !request.Values.ContainsKey(column)))
            {
                return Failure(
                    DatabaseDeliveryJobStatus.PermanentFailed,
                    "UniqueKeyValueMissing",
                    "A selected unique-key value is missing.");
            }

            await using var connection = connections.CreateConnection(
                request.ProfileId);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = timeoutSeconds;
            var selected = request.Values.Keys
                .OrderBy(column => columns[column].Ordinal)
                .ToArray();
            command.CommandText =
                $"SELECT {string.Join(", ", selected.Select(Quote))} " +
                $"FROM {Quote(request.SchemaSnapshot.Schema)}.{Quote(request.SchemaSnapshot.Table)} " +
                $"WHERE {string.Join(" AND ", key.Columns.Select((column, index) => $"{Quote(column)} = @k{index}"))}";
            for (var index = 0; index < key.Columns.Count; index++)
            {
                var column = columns[key.Columns[index]];
                AddParameter(
                    command,
                    $"@k{index}",
                    new PreparedValue(
                        column,
                        ConvertValue(request.Values[column.Name], column)));
            }

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return Failure(
                    DatabaseDeliveryJobStatus.RetryableFailed,
                    "RowNotFoundAfterReconciliation",
                    "No matching target row exists after reconciliation.");
            }

            for (var index = 0; index < selected.Length; index++)
            {
                var column = columns[selected[index]];
                var expected = Canonical(
                    ConvertValue(request.Values[column.Name], column),
                    column.ScalarType);
                var actual = Canonical(
                    await reader.IsDBNullAsync(index, cancellationToken)
                        ? null
                        : reader.GetValue(index),
                    column.ScalarType);
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    return Failure(
                        DatabaseDeliveryJobStatus.DeliveryConflict,
                        "ExistingRowDiffers",
                        "An existing row has the same key but different mapped values.");
                }
            }

            return new DatabaseWriteResult(
                DatabaseDeliveryJobStatus.AlreadyDelivered);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is DbException or
                                           TimeoutException or
                                           DatabaseWriteValidationException)
        {
            return Failure(
                DatabaseDeliveryJobStatus.UnknownOutcome,
                "ReconciliationUnavailable",
                "Target reconciliation is unavailable.");
        }
    }

    private static object? ConvertValue(
        JsonElement value,
        TargetColumnSchema column)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        try
        {
            return column.ScalarType switch
            {
                DatabaseScalarType.Text => value.GetString(),
                DatabaseScalarType.Integer => value.ValueKind == JsonValueKind.Number
                    ? value.GetInt64()
                    : long.Parse(value.GetString()!, CultureInfo.InvariantCulture),
                DatabaseScalarType.Decimal => value.ValueKind == JsonValueKind.Number
                    ? value.GetDecimal()
                    : decimal.Parse(value.GetString()!, CultureInfo.InvariantCulture),
                DatabaseScalarType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? value.GetBoolean()
                    : bool.Parse(value.GetString()!),
                DatabaseScalarType.Date => DateTime.ParseExact(
                    value.GetString()!,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None),
                DatabaseScalarType.Time => TimeSpan.Parse(
                    value.GetString()!,
                    CultureInfo.InvariantCulture),
                DatabaseScalarType.DateTime => ConvertDateTime(
                    value.GetString()!,
                    column.ProviderType),
                DatabaseScalarType.Guid => Guid.Parse(value.GetString()!),
                DatabaseScalarType.Binary => Convert.FromBase64String(
                    value.GetString()!),
                _ => throw new FormatException()
            };
        }
        catch (Exception exception) when (exception is FormatException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            throw new DatabaseWriteValidationException(
                "InvalidTypedValue",
                "Mapped value cannot be converted to the target type.");
        }
    }

    private static object ConvertDateTime(string value, string providerType)
    {
        var parsed = DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
        return providerType.ToLowerInvariant() switch
        {
            "timestamp" or "datetime" or "datetime2" or "smalldatetime" =>
                DateTime.SpecifyKind(parsed.DateTime, DateTimeKind.Unspecified),
            _ => parsed
        };
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        PreparedValue value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value.Value ?? DBNull.Value;
        parameter.DbType = value.Column.ScalarType switch
        {
            DatabaseScalarType.Text => DbType.String,
            DatabaseScalarType.Integer => DbType.Int64,
            DatabaseScalarType.Decimal => DbType.Decimal,
            DatabaseScalarType.Boolean => DbType.Boolean,
            DatabaseScalarType.Date => DbType.Date,
            DatabaseScalarType.Time => DbType.Time,
            DatabaseScalarType.DateTime => value.Value is DateTimeOffset
                ? DbType.DateTimeOffset
                : DbType.DateTime,
            DatabaseScalarType.Guid => DbType.Guid,
            DatabaseScalarType.Binary => DbType.Binary,
            _ => DbType.Object
        };
        if (value.Column.Length is > 0)
        {
            parameter.Size = value.Column.Length.Value;
        }
        if (parameter is IDbDataParameter typed)
        {
            if (value.Column.Precision is > 0 and <= 255)
            {
                typed.Precision = (byte)value.Column.Precision.Value;
            }
            if (value.Column.Scale is >= 0 and <= 255)
            {
                typed.Scale = (byte)value.Column.Scale.Value;
            }
        }
        command.Parameters.Add(parameter);
    }

    private static string Canonical(object? value, DatabaseScalarType type)
    {
        if (value is null || value is DBNull)
        {
            return "null";
        }
        return type switch
        {
            DatabaseScalarType.Decimal => Convert.ToDecimal(
                value,
                CultureInfo.InvariantCulture).ToString("G29", CultureInfo.InvariantCulture),
            DatabaseScalarType.Date => Convert.ToDateTime(
                value,
                CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DatabaseScalarType.Time => value is TimeSpan span
                ? span.ToString("c", CultureInfo.InvariantCulture)
                : Convert.ToString(value, CultureInfo.InvariantCulture)!,
            DatabaseScalarType.DateTime => value is DateTimeOffset offset
                ? offset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : new DateTimeOffset(Convert.ToDateTime(value, CultureInfo.InvariantCulture))
                    .ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DatabaseScalarType.Binary => Convert.ToBase64String((byte[])value),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static async Task SafeRollbackAsync(DbTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (DbException)
        {
        }
    }

    private static DatabaseWriteResult Failure(
        string status,
        string code,
        string message) => new(status, code, message);

    private sealed record PreparedWrite(
        string Sql,
        IReadOnlyList<PreparedValue> Values);

    private sealed record PreparedValue(
        TargetColumnSchema Column,
        object? Value);
}

public enum ProviderFailure
{
    Unique,
    Schema,
    Permanent,
    Transient
}

public sealed class DatabaseWriteValidationException(
    string code,
    string message) : Exception(message)
{
    public string Code { get; } = code;
}
