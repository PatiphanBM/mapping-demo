using System.Text.Json;
using Dapper;
using MappingDemo.Api.Contracts.Tables;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class TableService
{
    private const string SelectTablesSql = """
        WITH selected_tables AS (
            SELECT *
            FROM table_definitions
            WHERE (@Search IS NULL OR name ILIKE @Search)
              AND (@Kind IS NULL OR kind = @Kind)
              AND (@Status IS NULL OR verification_status = @Status)
            ORDER BY id
            LIMIT @RowLimit OFFSET @RowOffset
        )
        SELECT
            selected_tables.id AS "Id",
            selected_tables.name AS "Name",
            selected_tables.kind AS "Kind",
            selected_tables.created_at AS "CreatedAt",
            selected_tables.verification_status AS "VerificationStatus",
            selected_tables.verification_checked_at AS "VerificationCheckedAt",
            table_columns.name AS "ColumnName",
            table_columns.data_type AS "DataType",
            table_columns.is_required AS "IsRequired",
            table_columns.ordinal AS "Ordinal",
            table_columns.length AS "Length",
            table_columns.precision AS "Precision",
            table_columns.scale AS "Scale"
        FROM selected_tables
        INNER JOIN table_columns
            ON table_columns.table_id = selected_tables.id
        ORDER BY selected_tables.id, table_columns.ordinal;
        """;

    private const string SelectTableByIdSql = """
        SELECT
            table_definitions.id AS "Id",
            table_definitions.name AS "Name",
            table_definitions.kind AS "Kind",
            table_definitions.created_at AS "CreatedAt",
            table_definitions.verification_status AS "VerificationStatus",
            table_definitions.verification_checked_at AS "VerificationCheckedAt",
            table_columns.name AS "ColumnName",
            table_columns.data_type AS "DataType",
            table_columns.is_required AS "IsRequired",
            table_columns.ordinal AS "Ordinal",
            table_columns.length AS "Length",
            table_columns.precision AS "Precision",
            table_columns.scale AS "Scale"
        FROM table_definitions
        INNER JOIN table_columns
            ON table_columns.table_id = table_definitions.id
        WHERE table_definitions.id = @Id
        ORDER BY table_columns.ordinal;
        """;

    private const string InsertTableDefinitionSql = """
        INSERT INTO table_definitions (name, kind)
        VALUES (@Name, @Kind)
        RETURNING id;
        """;

    private const string InsertTableColumnSql = """
        INSERT INTO table_columns (
            table_id,
            name,
            data_type,
            is_required,
            ordinal,
            length,
            precision,
            scale)
        VALUES (
            @TableId,
            @Name,
            @DataType,
            @IsRequired,
            @Ordinal,
            @Length,
            @Precision,
            @Scale);
        """;

    private const string SelectTableNameForUpdateSql = """
        SELECT name
        FROM table_definitions
        WHERE id = @Id
        FOR UPDATE;
        """;

    private const string SelectNextOrdinalSql = """
        SELECT COALESCE(MAX(ordinal), 0) + 1
        FROM table_columns
        WHERE table_id = @TableId;
        """;

    private const string InsertAuditSql = """
        INSERT INTO table_admin_audit (
            table_id,
            action,
            outcome,
            operator_id,
            reason,
            correlation_id,
            safe_detail)
        VALUES (
            @TableId,
            @Action,
            @Outcome,
            @OperatorId,
            @Reason,
            @CorrelationId,
            CAST(@SafeDetail AS jsonb));
        """;

    private const string MarkVerifyingSql = """
        UPDATE table_definitions
        SET verification_status = 'Verifying'
        WHERE id = @TableId;
        """;

    private const string ResetVerificationSql = """
        UPDATE table_definitions
        SET verification_status = 'Unknown',
            verification_checked_at = NULL,
            verification_duration_ms = NULL
        WHERE id = @TableId;
        """;

    private const string SaveVerificationSql = """
        INSERT INTO table_schema_verifications (
            table_id,
            status,
            expected_schema,
            actual_schema,
            diagnostics,
            duration_ms,
            checked_at,
            operator_id,
            correlation_id)
        VALUES (
            @TableId,
            @Status,
            CAST(@ExpectedSchema AS jsonb),
            CAST(@ActualSchema AS jsonb),
            CAST(@Diagnostics AS jsonb),
            @DurationMilliseconds,
            @CheckedAt,
            @OperatorId,
            @CorrelationId);

        UPDATE table_definitions
        SET verification_status = @Status,
            verification_checked_at = @CheckedAt,
            verification_duration_ms = @DurationMilliseconds
        WHERE id = @TableId;
        """;

    private const string SelectLatestVerificationSql = """
        SELECT
            status AS "Status",
            expected_schema::text AS "ExpectedSchema",
            actual_schema::text AS "ActualSchema",
            diagnostics::text AS "Diagnostics",
            duration_ms AS "DurationMilliseconds",
            checked_at AS "CheckedAt"
        FROM table_schema_verifications
        WHERE table_id = @TableId
        ORDER BY checked_at DESC, id DESC
        LIMIT 1;
        """;

    private const string SelectReferencesSql = """
        SELECT
            config.id AS "ConfigId",
            config.name AS "ConfigName",
            CASE
                WHEN config.source_table_id = @TableId THEN 'Source'
                ELSE 'Normalized'
            END AS "Usage",
            version.id AS "ConfigVersionId",
            version.version_no AS "VersionNo",
            version.id = config.active_version_id AS "IsActiveVersion",
            COUNT(job.id) AS "PinnedJobCount"
        FROM mapping_configs AS config
        LEFT JOIN mapping_config_versions AS version
            ON version.config_id = config.id
        LEFT JOIN file_jobs AS job
            ON job.config_version_id = version.id
        WHERE config.source_table_id = @TableId
           OR config.normalized_table_id = @TableId
        GROUP BY
            config.id,
            config.name,
            config.source_table_id,
            version.id,
            version.version_no,
            config.active_version_id
        ORDER BY config.id, version.version_no;
        """;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly NpgsqlDataSource _dataSource;

    public TableService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public Task<IReadOnlyList<TableResponse>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        GetAllAsync(
            search: null,
            kind: null,
            status: null,
            limit: 100,
            offset: 0,
            cancellationToken);

    public async Task<IReadOnlyList<TableResponse>> GetAllAsync(
        string? search,
        TableKind? kind,
        TableVerificationStatus? status,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectTablesSql,
            new
            {
                Search = string.IsNullOrWhiteSpace(search)
                    ? null
                    : $"%{search.Trim()}%",
                Kind = kind?.ToString(),
                Status = status?.ToString(),
                RowLimit = limit,
                RowOffset = offset
            },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<TableRow>(command);

        return MapTables(rows);
    }

    public async Task<TableResponse?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectTableByIdSql,
            new { Id = id },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<TableRow>(command);

        return MapTables(rows).SingleOrDefault();
    }

    public Task<long> CreateAsync(
        TableDefinition definition,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(definition, audit: null, cancellationToken);

    public Task<long> CreateAsync(
        TableDefinition definition,
        TableAuditContext audit,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(definition, audit, cancellationToken);

    private async Task<long> CreateCoreAsync(
        TableDefinition definition,
        TableAuditContext? audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var tableId = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                InsertTableDefinitionSql,
                new
                {
                    definition.Name,
                    Kind = definition.Kind.ToString()
                },
                transaction,
                cancellationToken: cancellationToken));

        var columnParameters = definition.Columns.Select(column => new
        {
            TableId = tableId,
            column.Name,
            DataType = column.DataType.ToString(),
            column.IsRequired,
            column.Ordinal,
            column.Length,
            column.Precision,
            column.Scale
        });
        await connection.ExecuteAsync(new CommandDefinition(
            InsertTableColumnSql,
            columnParameters,
            transaction,
            cancellationToken: cancellationToken));

        var createTableSql = definition.Kind switch
        {
            TableKind.Source => DdlBuilder.CreateSourceTable(definition),
            TableKind.Normalized => DdlBuilder.CreateNormalizedTable(definition),
            _ => throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.Kind,
                "Unsupported table kind.")
        };
        await connection.ExecuteAsync(new CommandDefinition(
            createTableSql,
            transaction: transaction,
            cancellationToken: cancellationToken));

        if (audit is not null)
        {
            await InsertAuditAsync(
                connection,
                transaction,
                tableId,
                "Create",
                "Success",
                audit,
                new { definition.Kind, ColumnCount = definition.Columns.Count },
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return tableId;
    }

    public Task<bool> AddColumnAsync(
        long tableId,
        ColumnRequest request,
        CancellationToken cancellationToken = default) =>
        AddColumnCoreAsync(tableId, request, audit: null, cancellationToken);

    public Task<bool> AddColumnAsync(
        long tableId,
        ColumnRequest request,
        TableAuditContext audit,
        CancellationToken cancellationToken = default) =>
        AddColumnCoreAsync(tableId, request, audit, cancellationToken);

    private async Task<bool> AddColumnCoreAsync(
        long tableId,
        ColumnRequest request,
        TableAuditContext? audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var tableName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                SelectTableNameForUpdateSql,
                new { Id = tableId },
                transaction,
                cancellationToken: cancellationToken));
        if (tableName is null)
        {
            return false;
        }

        var ordinal = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                SelectNextOrdinalSql,
                new { TableId = tableId },
                transaction,
                cancellationToken: cancellationToken));
        var column = new ColumnDefinition(
            request.Name,
            request.DataType,
            request.IsRequired,
            ordinal,
            request.Length,
            request.Precision,
            request.Scale);

        await connection.ExecuteAsync(new CommandDefinition(
            InsertTableColumnSql,
            new
            {
                TableId = tableId,
                column.Name,
                DataType = column.DataType.ToString(),
                column.IsRequired,
                column.Ordinal,
                column.Length,
                column.Precision,
                column.Scale
            },
            transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            DdlBuilder.AddColumn(tableName, column),
            transaction: transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            ResetVerificationSql,
            new { TableId = tableId },
            transaction,
            cancellationToken: cancellationToken));

        if (audit is not null)
        {
            await InsertAuditAsync(
                connection,
                transaction,
                tableId,
                "AddColumn",
                "Success",
                audit,
                new { column.DataType },
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkVerifyingAsync(
        long tableId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            MarkVerifyingSql,
            new { TableId = tableId },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task SaveVerificationAsync(
        long tableId,
        TableSchemaVerificationResult result,
        string? operatorId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            SaveVerificationSql,
            new
            {
                TableId = tableId,
                Status = result.Status.ToString(),
                ExpectedSchema = JsonSerializer.Serialize(
                    result.Expected,
                    JsonOptions),
                ActualSchema = result.Actual is null
                    ? null
                    : JsonSerializer.Serialize(result.Actual, JsonOptions),
                Diagnostics = JsonSerializer.Serialize(
                    result.Diagnostics,
                    JsonOptions),
                result.DurationMilliseconds,
                result.CheckedAt,
                OperatorId = operatorId,
                CorrelationId = correlationId
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<TableSchemaVerificationResult?>
        GetLatestVerificationAsync(
            long tableId,
            CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<VerificationRow>(
            new CommandDefinition(
                SelectLatestVerificationSql,
                new { TableId = tableId },
                cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        return new TableSchemaVerificationResult(
            Enum.Parse<TableVerificationStatus>(row.Status),
            JsonSerializer.Deserialize<TableSchemaSnapshot>(
                row.ExpectedSchema,
                JsonOptions)!,
            row.ActualSchema is null
                ? null
                : JsonSerializer.Deserialize<TableSchemaSnapshot>(
                    row.ActualSchema,
                    JsonOptions),
            JsonSerializer.Deserialize<TableSchemaDiagnostic[]>(
                row.Diagnostics,
                JsonOptions) ?? [],
            new DateTimeOffset(
                DateTime.SpecifyKind(row.CheckedAt, DateTimeKind.Utc)),
            row.DurationMilliseconds);
    }

    public async Task<IReadOnlyList<TableReferenceResponse>>
        GetReferencesAsync(
            long tableId,
            CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TableReferenceResponse>(
            new CommandDefinition(
                SelectReferencesSql,
                new { TableId = tableId },
                cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task RecordFailureAuditAsync(
        long? tableId,
        string action,
        TableAuditContext audit,
        string code,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await InsertAuditAsync(
            connection,
            transaction: null,
            tableId,
            action,
            "Failure",
            audit,
            new { Code = code },
            cancellationToken);
    }

    private static async Task InsertAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long? tableId,
        string action,
        string outcome,
        TableAuditContext audit,
        object safeDetail,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            InsertAuditSql,
            new
            {
                TableId = tableId,
                Action = action,
                Outcome = outcome,
                audit.OperatorId,
                audit.Reason,
                audit.CorrelationId,
                SafeDetail = JsonSerializer.Serialize(
                    safeDetail,
                    JsonOptions)
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static IReadOnlyList<TableResponse> MapTables(
        IEnumerable<TableRow> rows) =>
        rows.GroupBy(row => row.Id)
            .Select(group =>
            {
                var table = group.First();
                var columns = group.OrderBy(row => row.Ordinal)
                    .Select(row => new ColumnRequest(
                        row.ColumnName,
                        Enum.Parse<ColumnDataType>(row.DataType),
                        row.IsRequired,
                        row.Length,
                        row.Precision,
                        row.Scale))
                    .ToArray();

                return new TableResponse(
                    table.Id,
                    table.Name,
                    Enum.Parse<TableKind>(table.Kind),
                    columns,
                    table.CreatedAt,
                    Enum.Parse<TableVerificationStatus>(
                        table.VerificationStatus),
                    table.VerificationCheckedAt);
            })
            .ToArray();

    private sealed class TableRow
    {
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public string VerificationStatus { get; init; } = string.Empty;
        public DateTimeOffset? VerificationCheckedAt { get; init; }
        public string ColumnName { get; init; } = string.Empty;
        public string DataType { get; init; } = string.Empty;
        public bool IsRequired { get; init; }
        public int Ordinal { get; init; }
        public int? Length { get; init; }
        public int? Precision { get; init; }
        public int? Scale { get; init; }
    }

    private sealed record VerificationRow(
        string Status,
        string ExpectedSchema,
        string? ActualSchema,
        string Diagnostics,
        double DurationMilliseconds,
        DateTime CheckedAt);
}
