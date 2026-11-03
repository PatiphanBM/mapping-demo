using System.Text.Json;
using Dapper;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Options;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MappingDemo.Tests.MappingConfigs;

public sealed class DestinationDefinitionMigrationIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Migration_backfills_legacy_and_keeps_old_application_compatible()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"destination_test_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();

        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var testConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(
                testConnectionString);
            await using var connection = await dataSource.OpenConnectionAsync();
            await connection.ExecuteAsync(
                await ReadMigrationAsync("0001_table_definitions.sql"));
            await connection.ExecuteAsync(
                await ReadMigrationAsync("0002_mapping_configs.sql"));

            var tableIds = (await connection.QueryAsync<long>("""
                INSERT INTO table_definitions (name, kind)
                VALUES ('source_orders', 'Source'),
                       ('normalized_orders', 'Normalized')
                RETURNING id;
                """)).ToArray();
            var configId = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO mapping_configs (
                    name, input_folder, source_table_id,
                    normalized_table_id)
                VALUES (
                    'Orders', 'orders-in', @SourceTableId,
                    @NormalizedTableId)
                RETURNING id;
                """, new
            {
                SourceTableId = tableIds[0],
                NormalizedTableId = tableIds[1]
            });
            var versionId = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO mapping_config_versions (
                    config_id, version_no, input_layout,
                    transformation_program)
                VALUES (@ConfigId, 1, '{}'::jsonb, '{}'::jsonb)
                RETURNING id;
                """, new { ConfigId = configId });

            await connection.ExecuteAsync(
                await ReadMigrationAsync("0007_destination_definition.sql"));

            var destinationJson = await connection.ExecuteScalarAsync<string>(
                """
                SELECT destination_definition::text
                FROM mapping_config_versions
                WHERE id = @VersionId;
                """,
                new { VersionId = versionId });
            Assert.NotNull(destinationJson);
            using var destination = JsonDocument.Parse(destinationJson);
            Assert.Empty(DestinationDefinitionValidator.Validate(
                destination.RootElement));
            Assert.Equal(
                "legacyTable",
                destination.RootElement.GetProperty("kind").GetString());
            Assert.Equal(
                tableIds[1],
                destination.RootElement
                    .GetProperty("normalizedTableId")
                    .GetInt64());

            var columns = (await connection.QueryAsync<ColumnState>("""
                SELECT
                    table_name AS "TableName",
                    column_name AS "Name",
                    is_nullable AS "IsNullable"
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND (
                    (table_name = 'mapping_configs'
                     AND column_name = 'normalized_table_id')
                    OR
                    (table_name = 'mapping_config_versions'
                     AND column_name IN (
                        'destination_definition',
                        'api_destination_profile_id',
                        'contract_reference',
                        'contract_schema_snapshot',
                        'contract_schema_hash',
                        'contract_schema_fetched_at',
                        'payload_bindings_snapshot')))
                ORDER BY table_name, column_name;
                """)).ToArray();
            Assert.Equal(8, columns.Length);
            Assert.All(columns, column =>
                Assert.Equal("YES", column.IsNullable));
            Assert.True(await connection.ExecuteScalarAsync<bool>("""
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conrelid = 'mapping_configs'::regclass
                      AND confrelid = 'table_definitions'::regclass
                      AND contype = 'f');
                """));
            Assert.True(await connection.ExecuteScalarAsync<bool>("""
                SELECT to_regclass(
                    'ix_mapping_config_versions_destination_kind')
                    IS NOT NULL;
                """));

            await connection.ExecuteAsync(
                await ReadMigrationAsync("0008_input_file_extensions.sql"));
            var acceptedExtensions = await connection.QuerySingleAsync<string[]>("""
                SELECT ARRAY(
                    SELECT jsonb_array_elements_text(accepted_extensions)
                    ORDER BY 1)
                FROM mapping_config_versions
                WHERE id = @VersionId;
                """, new { VersionId = versionId });
            Assert.Equal([".csv", ".txt"], acceptedExtensions);

            var beforeExpand = await connection.QuerySingleAsync<RowSnapshot>(
                """
                SELECT
                    xmin::text AS "VersionToken",
                    destination_definition::text AS "DestinationJson"
                FROM mapping_config_versions
                WHERE id = @VersionId;
                """,
                new { VersionId = versionId });

            await connection.ExecuteAsync(
                await ReadMigrationAsync("0011_json_file_destination.sql"));

            await connection.ExecuteAsync("""
                CREATE TABLE delivery_jobs (id bigint PRIMARY KEY);
                """);
            await connection.ExecuteAsync(
                await ReadMigrationAsync(
                    "0014_api_destination_profile_catalog.sql"));

            var afterExpand = await connection.QuerySingleAsync<RowSnapshot>(
                """
                SELECT
                    xmin::text AS "VersionToken",
                    destination_definition::text AS "DestinationJson"
                FROM mapping_config_versions
                WHERE id = @VersionId;
                """,
                new { VersionId = versionId });
            Assert.Equal(beforeExpand, afterExpand);

            var legacyService = new MappingConfigService(
                dataSource,
                Options.Create(new PathOptions { InputRoot = "input" }),
                new TestHostEnvironment());
            var createdByOldApplication = await legacyService
                .CreateVersionAsync(
                    configId,
                    OldApplicationRequest(),
                    CancellationToken.None);
            var loadedByOldApplication = await legacyService.GetVersionAsync(
                configId,
                createdByOldApplication.VersionNo,
                CancellationToken.None);
            Assert.NotNull(loadedByOldApplication);
            Assert.Equal(
                "default",
                loadedByOldApplication.TransformationProgram
                    .Outputs.Single().OutputKey);

            var jsonConfigId = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO mapping_configs (
                    name, input_folder, source_table_id,
                    normalized_table_id)
                VALUES (
                    'Future JSON', 'future-json-in', @SourceTableId, NULL)
                RETURNING id;
                """, new { SourceTableId = tableIds[0] });
            var jsonVersionId = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO mapping_config_versions (
                    config_id,
                    version_no,
                    input_layout,
                    transformation_program,
                    destination_definition,
                    api_destination_profile_id)
                VALUES (
                    @ConfigId,
                    1,
                    '{}'::jsonb,
                    '{}'::jsonb,
                    '{
                      "kind":"jsonFile",
                      "fileDestinationProfileId":"local-json-export",
                      "contract":{
                        "id":"proposal",
                        "version":"1",
                        "reference":"proposal/1",
                        "schemaHash":"sha256:test"
                      },
                      "relativeFolder":"customers",
                      "fileNameTemplate":"{sourceBaseName}.{fileJobId}.json",
                      "formatting":"compact"
                    }'::jsonb,
                    NULL)
                RETURNING id;
                """, new { ConfigId = jsonConfigId });
            var jsonRow = await connection.QuerySingleAsync<JsonDestinationRow>(
                """
                SELECT
                    destination_definition::text AS "DestinationJson",
                    api_destination_profile_id AS "ApiDestinationProfileId"
                FROM mapping_config_versions
                WHERE id = @VersionId;
                """,
                new { VersionId = jsonVersionId });
            Assert.Null(jsonRow.ApiDestinationProfileId);
            var jsonDefinition =
                MappingDemo.Shared.Serialization.PolymorphicJson
                    .Deserialize<MappingDemo.Shared.Destinations.DestinationDefinition>(
                        jsonRow.DestinationJson,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.IsType<MappingDemo.Shared.Destinations.JsonFileDestination>(
                jsonDefinition);

            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                connection.ExecuteAsync("""
                    UPDATE mapping_config_versions
                    SET destination_definition = '{"kind":"queue"}'::jsonb
                    WHERE id = @VersionId;
                    """, new { VersionId = versionId }));
            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal(
                "mapping_config_versions_destination_kind_check",
                exception.ConstraintName);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static CreateMappingConfigVersionRequest OldApplicationRequest()
    {
        return new CreateMappingConfigVersionRequest(
            new DelimitedInputLayout(
                "utf-8",
                ",",
                PreambleLines: 0,
                DelimitedHeaderPolicy.SingleRow,
                [new DelimitedInputField("Order No", "order_no")]),
            new TransformationProgram
            {
                LanguageVersion = 1,
                SemanticsProfile =
                    TransformationCapabilities.SqlServerLegacyProfile,
                Outputs =
                [
                    new TransformationOutput
                    {
                        OutputKey = "default",
                        Fields =
                        [
                            new FieldAssignment
                            {
                                NormalizedColumn = "order_no",
                                Value = new SourceExpression
                                {
                                    Field = "order_no"
                                }
                            }
                        ]
                    }
                ]
            });
    }

    private static async Task<string> ReadMigrationAsync(string fileName)
    {
        var resourceName =
            "MappingDemo.Shared.Database.Migrations." + fileName;
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }

    private sealed record ColumnState(
        string TableName,
        string Name,
        string IsNullable);

    private sealed record RowSnapshot(
        string VersionToken,
        string DestinationJson);

    private sealed record JsonDestinationRow(
        string DestinationJson,
        string? ApiDestinationProfileId);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "MappingDemo.Tests";

        public string ContentRootPath { get; set; } =
            AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
