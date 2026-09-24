using MappingDemo.Shared.Tables;

namespace MappingDemo.Tests.Tables;

public sealed class DdlBuilderTests
{
    [Fact]
    public void CreateSourceTable_builds_source_columns_in_ordinal_order()
    {
        var definition = new TableDefinition(
            "src_orders",
            TableKind.Source,
            [
                new ColumnDefinition(
                    "customer_name",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 2),
                new ColumnDefinition(
                    "order_no",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 1)
            ]);
        var expected = """
            CREATE TABLE "src_orders" (
                id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                file_job_id bigint NOT NULL,
                row_number int NOT NULL,
                imported_at timestamptz NOT NULL DEFAULT now(),
                "order_no" text,
                "customer_name" text,
                UNIQUE (file_job_id, row_number)
            );
            """;

        var sql = DdlBuilder.CreateSourceTable(definition);

        Assert.Equal(
            expected.ReplaceLineEndings("\n"),
            sql.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void CreateNormalizedTable_maps_data_types_and_keeps_data_columns_nullable()
    {
        var definition = new TableDefinition(
            "norm_orders",
            TableKind.Normalized,
            [
                new ColumnDefinition(
                    "is_paid",
                    ColumnDataType.Boolean,
                    IsRequired: true,
                    Ordinal: 4),
                new ColumnDefinition(
                    "order_no",
                    ColumnDataType.Text,
                    IsRequired: true,
                    Ordinal: 1),
                new ColumnDefinition(
                    "order_date",
                    ColumnDataType.Date,
                    IsRequired: true,
                    Ordinal: 2),
                new ColumnDefinition(
                    "amount",
                    ColumnDataType.Decimal,
                    IsRequired: true,
                    Ordinal: 3)
            ]);
        var expected = """
            CREATE TABLE "norm_orders" (
                id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                source_row_id bigint NOT NULL UNIQUE,
                file_job_id bigint NOT NULL,
                row_number int NOT NULL,
                row_job_id bigint NOT NULL,
                config_version_id bigint NOT NULL,
                normalized_at timestamptz NOT NULL DEFAULT now(),
                "order_no" text,
                "order_date" date,
                "amount" numeric,
                "is_paid" boolean
            );
            """;

        var sql = DdlBuilder.CreateNormalizedTable(definition);

        Assert.Equal(
            expected.ReplaceLineEndings("\n"),
            sql.ReplaceLineEndings("\n"));
    }
}
