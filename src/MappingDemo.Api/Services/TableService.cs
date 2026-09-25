using Dapper;
using MappingDemo.Api.Contracts.Tables;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class TableService
{
    private const string SelectTablesSql = """
        SELECT
            table_definitions.id AS "Id",
            table_definitions.name AS "Name",
            table_definitions.kind AS "Kind",
            table_columns.name AS "ColumnName",
            table_columns.data_type AS "DataType",
            table_columns.is_required AS "IsRequired",
            table_columns.ordinal AS "Ordinal"
        FROM table_definitions
        INNER JOIN table_columns
            ON table_columns.table_id = table_definitions.id
        ORDER BY table_definitions.id, table_columns.ordinal;
        """;

    private const string SelectTableByIdSql = """
        SELECT
            table_definitions.id AS "Id",
            table_definitions.name AS "Name",
            table_definitions.kind AS "Kind",
            table_columns.name AS "ColumnName",
            table_columns.data_type AS "DataType",
            table_columns.is_required AS "IsRequired",
            table_columns.ordinal AS "Ordinal"
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
            ordinal)
        VALUES (
            @TableId,
            @Name,
            @DataType,
            @IsRequired,
            @Ordinal);
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

    private readonly NpgsqlDataSource _dataSource;

    public TableService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<TableResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectTablesSql,
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

    public async Task<long> CreateAsync(
        TableDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var insertDefinitionCommand = new CommandDefinition(
            InsertTableDefinitionSql,
            new
            {
                definition.Name,
                Kind = definition.Kind.ToString()
            },
            transaction,
            cancellationToken: cancellationToken);
        var tableId = await connection.ExecuteScalarAsync<long>(
            insertDefinitionCommand);

        var columnParameters = definition.Columns.Select(column => new
        {
            TableId = tableId,
            column.Name,
            DataType = column.DataType.ToString(),
            column.IsRequired,
            column.Ordinal
        });
        var insertColumnsCommand = new CommandDefinition(
            InsertTableColumnSql,
            columnParameters,
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(insertColumnsCommand);

        var createTableSql = definition.Kind switch
        {
            TableKind.Source => DdlBuilder.CreateSourceTable(definition),
            TableKind.Normalized => DdlBuilder.CreateNormalizedTable(definition),
            _ => throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.Kind,
                "Unsupported table kind.")
        };
        var createTableCommand = new CommandDefinition(
            createTableSql,
            transaction: transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(createTableCommand);

        await transaction.CommitAsync(cancellationToken);

        return tableId;
    }

    public async Task<bool> AddColumnAsync(
        long tableId,
        ColumnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var selectTableCommand = new CommandDefinition(
            SelectTableNameForUpdateSql,
            new { Id = tableId },
            transaction,
            cancellationToken: cancellationToken);
        var tableName = await connection.QuerySingleOrDefaultAsync<string>(
            selectTableCommand);

        if (tableName is null)
        {
            return false;
        }

        var selectOrdinalCommand = new CommandDefinition(
            SelectNextOrdinalSql,
            new { TableId = tableId },
            transaction,
            cancellationToken: cancellationToken);
        var ordinal = await connection.ExecuteScalarAsync<int>(
            selectOrdinalCommand);
        var column = new ColumnDefinition(
            request.Name,
            request.DataType,
            request.IsRequired,
            ordinal);

        var insertColumnCommand = new CommandDefinition(
            InsertTableColumnSql,
            new
            {
                TableId = tableId,
                column.Name,
                DataType = column.DataType.ToString(),
                column.IsRequired,
                column.Ordinal
            },
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(insertColumnCommand);

        var addColumnCommand = new CommandDefinition(
            DdlBuilder.AddColumn(tableName, column),
            transaction: transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(addColumnCommand);

        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private static IReadOnlyList<TableResponse> MapTables(
        IEnumerable<TableRow> rows)
    {
        return rows
            .GroupBy(row => row.Id)
            .Select(group =>
            {
                var table = group.First();
                var columns = group
                    .OrderBy(row => row.Ordinal)
                    .Select(row => new ColumnRequest(
                        row.ColumnName,
                        Enum.Parse<ColumnDataType>(row.DataType),
                        row.IsRequired))
                    .ToArray();

                return new TableResponse(
                    table.Id,
                    table.Name,
                    Enum.Parse<TableKind>(table.Kind),
                    columns);
            })
            .ToArray();
    }

    private sealed class TableRow
    {
        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Kind { get; init; } = string.Empty;

        public string ColumnName { get; init; } = string.Empty;

        public string DataType { get; init; } = string.Empty;

        public bool IsRequired { get; init; }

        public int Ordinal { get; init; }
    }
}
