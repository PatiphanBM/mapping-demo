using Dapper;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Messaging;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Tests.Destinations;

public sealed class JsonExportPersistenceIntegrationTests
{
    [Fact]
    public async Task Migration_is_expand_only()
    {
        var sql = await ReadMigrationAsync(
            "0012_json_export_persistence.sql");

        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Migration_adds_export_storage_without_rewriting_existing_configs()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.NotEqual(
                "true",
                Environment.GetEnvironmentVariable(
                    "MAPPING_REQUIRE_POSTGRES_INTEGRATION"));
            return;
        }

        var schema = $"json_export_migration_{Guid.NewGuid():N}"[..40];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();

        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var scopedConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(
                scopedConnectionString);
            await using var connection = await dataSource.OpenConnectionAsync();

            foreach (var migration in MigrationsThrough0011)
            {
                await connection.ExecuteAsync(await ReadMigrationAsync(migration));
            }

            await connection.ExecuteAsync("""
                INSERT INTO table_definitions (id, name, kind)
                OVERRIDING SYSTEM VALUE
                VALUES
                    (1, 'source_rows', 'Source'),
                    (2, 'normalized_rows', 'Normalized');
                INSERT INTO mapping_configs (
                    id, name, input_folder, source_table_id,
                    normalized_table_id)
                OVERRIDING SYSTEM VALUE
                VALUES (1, 'legacy', 'legacy-in', 1, 2);
                INSERT INTO mapping_config_versions (
                    id, config_id, version_no, input_layout,
                    transformation_program, destination_definition)
                OVERRIDING SYSTEM VALUE
                VALUES (
                    1,
                    1,
                    1,
                    '{}'::jsonb,
                    '{}'::jsonb,
                    '{"kind":"legacyTable","normalizedTableId":2}'::jsonb);
                """);
            var before = await connection.ExecuteScalarAsync<string>("""
                SELECT destination_definition::text
                FROM mapping_config_versions
                WHERE id = 1;
                """);

            await connection.ExecuteAsync(
                await ReadMigrationAsync("0012_json_export_persistence.sql"));

            Assert.Equal(
                before,
                await connection.ExecuteScalarAsync<string>("""
                    SELECT destination_definition::text
                    FROM mapping_config_versions
                    WHERE id = 1;
                    """));
            Assert.Equal(
                2,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM information_schema.tables
                    WHERE table_schema = current_schema()
                      AND table_name IN (
                          'json_export_jobs',
                          'json_export_items');
                    """));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Storage_supports_idempotent_stage_ordered_reads_and_revisions()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.NotEqual(
                "true",
                Environment.GetEnvironmentVariable(
                    "MAPPING_REQUIRE_POSTGRES_INTEGRATION"));
            return;
        }

        var schema = $"json_export_storage_{Guid.NewGuid():N}"[..38];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();

        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var scopedConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(
                scopedConnectionString);
            await new MigrationRunner(dataSource).RunAsync();
            await using var connection = await dataSource.OpenConnectionAsync();
            await SeedExecutionAsync(connection);

            var firstExportJobId = await InsertExportJobAsync(
                connection,
                revision: 1);
            var ctpId = await UpsertItemAsync(
                connection,
                firstExportJobId,
                revision: 1,
                rowJobId: 10,
                sourceRowId: 100,
                rowNumber: 2,
                outputKey: "ctp",
                outputOrdinal: 1,
                payloadHash: "sha256:ctp");
            var pmxId = await UpsertItemAsync(
                connection,
                firstExportJobId,
                revision: 1,
                rowJobId: 11,
                sourceRowId: 101,
                rowNumber: 1,
                outputKey: "pmx",
                outputOrdinal: 0,
                payloadHash: "sha256:pmx-before");
            var duplicatePmxId = await UpsertItemAsync(
                connection,
                firstExportJobId,
                revision: 1,
                rowJobId: 11,
                sourceRowId: 101,
                rowNumber: 1,
                outputKey: "pmx",
                outputOrdinal: 0,
                payloadHash: "sha256:pmx-after");

            Assert.Equal(pmxId, duplicatePmxId);
            Assert.Equal(
                2,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM json_export_items
                    WHERE json_export_job_id = @ExportJobId;
                    """, new { ExportJobId = firstExportJobId }));
            Assert.Equal(
                "sha256:pmx-after",
                await connection.ExecuteScalarAsync<string>("""
                    SELECT payload_sha256
                    FROM json_export_items
                    WHERE id = @Id;
                    """, new { Id = pmxId }));

            var ordered = (await connection.QueryAsync<OrderedItem>("""
                SELECT
                    row_number AS "RowNumber",
                    output_ordinal AS "OutputOrdinal",
                    output_key AS "OutputKey",
                    id AS "Id"
                FROM json_export_items
                WHERE json_export_job_id = @ExportJobId
                ORDER BY row_number, output_ordinal, output_key, id;
                """, new { ExportJobId = firstExportJobId })).ToArray();
            Assert.Equal(
                [
                    new OrderedItem(1, 0, "pmx", pmxId),
                    new OrderedItem(2, 1, "ctp", ctpId)
                ],
                ordered);

            await connection.ExecuteAsync("SET enable_seqscan = off;");
            var plan = string.Join(
                Environment.NewLine,
                await connection.QueryAsync<string>("""
                    EXPLAIN (COSTS OFF)
                    SELECT id, payload_ciphertext
                    FROM json_export_items
                    WHERE json_export_job_id = @ExportJobId
                    ORDER BY row_number, output_ordinal, output_key, id;
                    """, new { ExportJobId = firstExportJobId }));
            Assert.Contains(
                "ix_json_export_items_finalize_order",
                plan,
                StringComparison.Ordinal);

            var secondExportJobId = await InsertExportJobAsync(
                connection,
                revision: 2,
                previousExportJobId: firstExportJobId);
            await UpsertItemAsync(
                connection,
                secondExportJobId,
                revision: 2,
                rowJobId: 11,
                sourceRowId: 101,
                rowNumber: 1,
                outputKey: "pmx",
                outputOrdinal: 0,
                payloadHash: "sha256:pmx-reprocessed");
            Assert.Equal(
                2,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM json_export_items
                    WHERE source_row_id = 101
                      AND output_key = 'pmx';
                    """));

            var payload = System.Text.Json.JsonSerializer.Serialize(
                new JsonExportRequested(firstExportJobId));
            const string addOutboxSql = """
                INSERT INTO outbox (topic, message_key, payload)
                VALUES (
                    @Topic,
                    @MessageKey,
                    CAST(@Payload AS jsonb))
                ON CONFLICT (topic, message_key)
                    WHERE topic = 'mapping.json-export-finalize'
                DO NOTHING;
                """;
            var outboxParameters = new
            {
                Topic = Topics.JsonExportFinalize,
                MessageKey = firstExportJobId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                Payload = payload
            };
            await connection.ExecuteAsync(addOutboxSql, outboxParameters);
            await connection.ExecuteAsync(addOutboxSql, outboxParameters);
            Assert.Equal(
                1,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM outbox
                    WHERE topic = 'mapping.json-export-finalize';
                    """));

            await connection.ExecuteAsync("""
                UPDATE json_export_items
                SET payload_ciphertext = NULL,
                    payload_nonce = NULL,
                    payload_tag = NULL,
                    encryption_key_reference = NULL,
                    purged_at = now()
                WHERE id = @Id;
                """, new { Id = ctpId });
            Assert.True(await connection.ExecuteScalarAsync<bool>("""
                SELECT payload_ciphertext IS NULL
                   AND payload_sha256 = 'sha256:ctp'
                   AND purged_at IS NOT NULL
                FROM json_export_items
                WHERE id = @Id;
                """, new { Id = ctpId }));

            var invalidEncryption = await Assert.ThrowsAsync<PostgresException>(
                () => UpsertItemAsync(
                    connection,
                    secondExportJobId,
                    revision: 2,
                    rowJobId: 10,
                    sourceRowId: 100,
                    rowNumber: 2,
                    outputKey: "invalid-encryption",
                    outputOrdinal: 1,
                    payloadHash: "sha256:invalid",
                    nonce: new byte[11]));
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                invalidEncryption.SqlState);

            var incompleteLegalHold = await Assert.ThrowsAsync<PostgresException>(
                () => connection.ExecuteAsync("""
                    UPDATE json_export_jobs
                    SET legal_hold = true
                    WHERE id = @Id;
                    """, new { Id = firstExportJobId }));
            Assert.Equal(
                PostgresErrorCodes.CheckViolation,
                incompleteLegalHold.SqlState);
            await connection.ExecuteAsync("""
                UPDATE json_export_jobs
                SET legal_hold = true,
                    legal_hold_reason = 'Litigation hold',
                    legal_hold_owner = 'legal@example.test',
                    legal_hold_until = now() + interval '30 days'
                WHERE id = @Id;
                """, new { Id = firstExportJobId });
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static readonly string[] MigrationsThrough0011 =
    [
        "0001_table_definitions.sql",
        "0002_mapping_configs.sql",
        "0003_file_jobs_outbox.sql",
        "0004_row_jobs.sql",
        "0005_row_job_evaluation_time.sql",
        "0006_row_output_reconciliation.sql",
        "0007_destination_definition.sql",
        "0008_input_file_extensions.sql",
        "0009_api_delivery.sql",
        "0010_api_delivery_operations.sql",
        "0011_json_file_destination.sql"
    ];

    private static async Task<string> ReadMigrationAsync(string fileName)
    {
        var resourceName =
            "MappingDemo.Shared.Database.Migrations." + fileName;
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Migration resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task SeedExecutionAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync("""
            INSERT INTO table_definitions (id, name, kind)
            OVERRIDING SYSTEM VALUE
            VALUES (1, 'source_rows', 'Source');
            INSERT INTO mapping_configs (
                id, name, input_folder, source_table_id,
                normalized_table_id)
            OVERRIDING SYSTEM VALUE
            VALUES (1, 'json', 'json-in', 1, NULL);
            INSERT INTO mapping_config_versions (
                id, config_id, version_no, input_layout,
                transformation_program, destination_definition)
            OVERRIDING SYSTEM VALUE
            VALUES (
                1,
                1,
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
                  "relativeFolder":"exports",
                  "fileNameTemplate":"{fileJobId}.json",
                  "formatting":"compact"
                }'::jsonb);
            INSERT INTO file_jobs (
                id, config_id, config_version_id, original_path,
                file_name, intake_key, import_status, archive_status)
            OVERRIDING SYSTEM VALUE
            VALUES (
                1, 1, 1, 'input.json', 'input.json', 'intake-1',
                'Imported', 'Archived');
            INSERT INTO row_jobs (
                id, file_job_id, source_row_id, row_number,
                config_version_id, kind, status)
            OVERRIDING SYSTEM VALUE
            VALUES
                (10, 1, 100, 2, 1, 'Initial', 'Done'),
                (11, 1, 101, 1, 1, 'Initial', 'Done');
            """);
    }

    private static Task<long> InsertExportJobAsync(
        NpgsqlConnection connection,
        int revision,
        long? previousExportJobId = null)
    {
        return connection.ExecuteScalarAsync<long>("""
            INSERT INTO json_export_jobs (
                file_job_id,
                config_version_id,
                destination_profile_id,
                artifact_revision,
                status,
                relative_temp_path,
                relative_final_path,
                previous_export_job_id)
            VALUES (
                1,
                1,
                'local-json-export',
                @Revision,
                'Pending',
                @TempPath,
                @FinalPath,
                @PreviousExportJobId)
            RETURNING id;
            """, new
        {
            Revision = revision,
            TempPath = $"exports/input.1.r{revision}.json.tmp",
            FinalPath = $"exports/input.1.r{revision}.json",
            PreviousExportJobId = previousExportJobId
        });
    }

    private static Task<long> UpsertItemAsync(
        NpgsqlConnection connection,
        long exportJobId,
        int revision,
        long rowJobId,
        long sourceRowId,
        int rowNumber,
        string outputKey,
        int outputOrdinal,
        string payloadHash,
        byte[]? nonce = null)
    {
        return connection.ExecuteScalarAsync<long>("""
            INSERT INTO json_export_items (
                json_export_job_id,
                file_job_id,
                config_version_id,
                artifact_revision,
                row_job_id,
                source_row_id,
                row_number,
                output_key,
                output_ordinal,
                payload_ciphertext,
                payload_nonce,
                payload_tag,
                encryption_key_reference,
                payload_sha256)
            VALUES (
                @ExportJobId,
                1,
                1,
                @Revision,
                @RowJobId,
                @SourceRowId,
                @RowNumber,
                @OutputKey,
                @OutputOrdinal,
                @Ciphertext,
                @Nonce,
                @Tag,
                'json-export-test-key',
                @PayloadHash)
            ON CONFLICT (
                file_job_id,
                config_version_id,
                artifact_revision,
                source_row_id,
                output_key)
            DO UPDATE SET
                row_job_id = EXCLUDED.row_job_id,
                row_number = EXCLUDED.row_number,
                output_ordinal = EXCLUDED.output_ordinal,
                payload_ciphertext = EXCLUDED.payload_ciphertext,
                payload_nonce = EXCLUDED.payload_nonce,
                payload_tag = EXCLUDED.payload_tag,
                encryption_key_reference = EXCLUDED.encryption_key_reference,
                payload_sha256 = EXCLUDED.payload_sha256,
                updated_at = now()
            RETURNING id;
            """, new
        {
            ExportJobId = exportJobId,
            Revision = revision,
            RowJobId = rowJobId,
            SourceRowId = sourceRowId,
            RowNumber = rowNumber,
            OutputKey = outputKey,
            OutputOrdinal = outputOrdinal,
            Ciphertext = new byte[] { 1, 2, 3 },
            Nonce = nonce ?? new byte[12],
            Tag = new byte[16],
            PayloadHash = payloadHash
        });
    }

    private sealed record OrderedItem(
        int RowNumber,
        int OutputOrdinal,
        string OutputKey,
        long Id);
}
