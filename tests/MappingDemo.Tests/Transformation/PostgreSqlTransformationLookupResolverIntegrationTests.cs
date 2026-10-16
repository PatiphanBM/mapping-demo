using Dapper;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using MappingDemo.Shared.Transformation.Adapters;
using Npgsql;

namespace MappingDemo.Tests.Transformation;

public sealed class PostgreSqlTransformationLookupResolverIntegrationTests
{
    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Batch_lookup_is_parameterized_and_uses_the_composite_index()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"lookup_test_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();

        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var testConnectionString = new NpgsqlConnectionStringBuilder(
                connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var testDataSource = NpgsqlDataSource.Create(
                testConnectionString);
            await using var setup = await testDataSource.OpenConnectionAsync();
            await setup.ExecuteAsync("""
                CREATE TABLE table_definitions (
                    id bigint PRIMARY KEY,
                    name text NOT NULL,
                    kind text NOT NULL
                );
                CREATE TABLE table_columns (
                    table_id bigint NOT NULL,
                    name text NOT NULL,
                    ordinal integer NOT NULL
                );
                CREATE TABLE mti_original (
                    source_row_id bigint NOT NULL,
                    system_id text NOT NULL,
                    cproductname text NOT NULL,
                    cloannumber text NOT NULL
                );
                CREATE INDEX ix_mti_original_policy_lookup
                    ON mti_original (system_id, cproductname, cloannumber);
                INSERT INTO table_definitions (id, name, kind)
                    VALUES (1, 'mti_original', 'Normalized');
                INSERT INTO table_columns (table_id, name, ordinal) VALUES
                    (1, 'system_id', 1),
                    (1, 'cproductname', 2),
                    (1, 'cloannumber', 3);
                INSERT INTO mti_original (
                    source_row_id, system_id, cproductname, cloannumber) VALUES
                    (1, 'SYS', 'PMX', 'LN-001'),
                    (2, 'SYS', 'PMX', 'LN-001'),
                    (3, 'SYS', 'CTP', 'LN-001');
                """);

            var resolver = new PostgreSqlTransformationLookupResolver(
                testDataSource);
            var validation = await resolver.ValidateAsync(
                TransformationCapabilities.MtiOriginalPolicyExistsV1);
            Assert.True(validation.IsValid, validation.Error);

            var validRequest = new TransformationLookupRequest(
                TransformationCapabilities.MtiOriginalPolicyExistsV1,
                "SYS",
                "PMX",
                "LN-001");
            var injectionRequest = validRequest with
            {
                LoanNumber = "LN-404' OR TRUE --"
            };
            var matches = await resolver.ResolveAsync(
                [validRequest, injectionRequest]);

            Assert.Equal(new long[] { 1, 2 }, matches
                .Where(match => match.Request == validRequest)
                .Select(match => match.SourceRowId)
                .OrderBy(sourceRowId => sourceRowId));
            Assert.DoesNotContain(
                matches,
                match => match.Request == injectionRequest);

            await setup.ExecuteAsync("SET enable_seqscan = off;");
            var plan = await setup.QueryAsync<string>("""
                EXPLAIN (FORMAT TEXT)
                WITH lookup_keys AS (
                    SELECT *
                    FROM unnest(
                        @SystemIds::text[],
                        @ProductNames::text[],
                        @LoanNumbers::text[])
                        AS keys(system_id, product_name, loan_number)
                )
                SELECT target.source_row_id
                FROM lookup_keys
                INNER JOIN mti_original AS target
                    ON target.system_id = lookup_keys.system_id
                    AND target.cproductname = lookup_keys.product_name
                    AND target.cloannumber = lookup_keys.loan_number;
                """, new
            {
                SystemIds = new[] { validRequest.SystemId },
                ProductNames = new[] { validRequest.ProductName },
                LoanNumbers = new[] { validRequest.LoanNumber }
            });
            Assert.Contains(
                plan,
                line => line.Contains(
                    "ix_mti_original_policy_lookup",
                    StringComparison.Ordinal));

            await setup.ExecuteAsync(
                "DROP INDEX ix_mti_original_policy_lookup;");
            var missingIndex = await resolver.ValidateAsync(
                TransformationCapabilities.MtiOriginalPolicyExistsV1);
            Assert.False(missingIndex.IsValid);
            Assert.Contains("composite index", missingIndex.Error);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }
}
