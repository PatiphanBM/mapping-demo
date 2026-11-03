using Dapper;
using System.Diagnostics;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Tables;
using Npgsql;

namespace MappingDemo.Tests.Contracts;

public sealed class VersionedContractRegistryTests
{
    private const string Schema = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["proposal"],
          "properties": {
            "proposal": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "insuredName": { "type": "string", "maxLength": 120 }
              }
            }
          }
        }
        """;

    private const string BreakingSchema = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "proposal": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "policyNumber": { "type": "string" }
              }
            }
          }
        }
        """;

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Bundled_and_postgres_adapters_resolve_equal_canonical_contract()
    {
        await WithRegistryAsync(async dataSource =>
        {
            var source = new PostgreSqlVersionedContractRegistry(dataSource);
            var postgres = new VersionedContractRegistryModule(source);
            var created = await postgres.ExecuteAsync(Create("1", Schema));
            Assert.True(created.IsSuccess, DiagnosticText(created.Diagnostics));
            var validation = await postgres.ValidateAsync(
                created.Version!.ContractVersionId);
            Assert.True(validation.IsValid,
                DiagnosticText(validation.Diagnostics));
            var published = await postgres.ExecuteAsync(
                new PublishContractVersionCommand(
                    created.Version.ContractVersionId,
                    created.Version.ConcurrencyToken,
                    false,
                    "operator-a",
                    "Publish initial proposal contract",
                    "publish-proposal-1"));
            Assert.True(published.IsSuccess,
                DiagnosticText(published.Diagnostics));

            var bundled = new VersionedContractRegistryModule(
                new BundledVersionedContractRegistry([
                    new BundledContractDefinition(
                        "proposal",
                        "Proposal",
                        "1",
                        Schema,
                        DateTimeOffset.Parse("2026-11-02T00:00:00Z"))
                ]));
            var postgresResolved = await postgres.ResolveExactAsync(
                "proposal/1");
            var bundledResolved = await bundled.ResolveExactAsync(
                "proposal/1");

            Assert.True(postgresResolved.IsSuccess,
                DiagnosticText(postgresResolved.Diagnostics));
            Assert.True(bundledResolved.IsSuccess,
                DiagnosticText(bundledResolved.Diagnostics));
            Assert.Equal(
                bundledResolved.ContractVersion!.Version.CanonicalSchema,
                postgresResolved.ContractVersion!.Version.CanonicalSchema);
            Assert.Equal(
                bundledResolved.ContractVersion.Version.SchemaHash,
                postgresResolved.ContractVersion.Version.SchemaHash);
            Assert.Equal(
                bundledResolved.ContractVersion.PathCatalog,
                postgresResolved.ContractVersion.PathCatalog);
            Assert.Equal(
                ["proposal/1"],
                (await postgres.ListPublishedAsync())
                    .Select(item => item.Reference));
            var workerRegistry = new VersionedContractRegistryModule(
                new PostgreSqlVersionedContractRegistry(dataSource));
            var workerResolved = await workerRegistry.ResolveExactAsync(
                "proposal/1");
            Assert.Equal(
                postgresResolved.ContractVersion.Version.SchemaHash,
                workerResolved.ContractVersion!.Version.SchemaHash);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Breaking_publish_requires_acknowledgement_and_retire_keeps_exact_resolution()
    {
        await WithRegistryAsync(async dataSource =>
        {
            var registry = new VersionedContractRegistryModule(
                new PostgreSqlVersionedContractRegistry(dataSource));
            var first = await registry.ExecuteAsync(Create("1", Schema));
            var firstPublished = await registry.ExecuteAsync(
                new PublishContractVersionCommand(
                    first.Version!.ContractVersionId,
                    first.Version.ConcurrencyToken,
                    false,
                    "operator-a",
                    "Publish initial proposal contract",
                    "publish-first"));
            Assert.True(firstPublished.IsSuccess,
                DiagnosticText(firstPublished.Diagnostics));

            var clone = await registry.ExecuteAsync(
                new CloneContractVersionCommand(
                    firstPublished.Version!.ContractVersionId,
                    "2",
                    "operator-a",
                    "Clone proposal for breaking revision",
                    "clone-second"));
            var updated = await registry.ExecuteAsync(
                new UpdateContractDraftCommand(
                    clone.Version!.ContractVersionId,
                    clone.Version.ConcurrencyToken,
                    BreakingSchema,
                    "operator-a",
                    "Remove old path for breaking test",
                    "update-second"));
            var validation = await registry.ValidateAsync(
                updated.Version!.ContractVersionId);
            Assert.Equal(
                ContractCompatibility.Breaking,
                validation.Compatibility);

            var blocked = await registry.ExecuteAsync(
                new PublishContractVersionCommand(
                    updated.Version.ContractVersionId,
                    updated.Version.ConcurrencyToken,
                    false,
                    "operator-a",
                    "Attempt unacknowledged breaking publish",
                    "publish-second-blocked"));
            Assert.False(blocked.IsSuccess);
            Assert.Contains(blocked.Diagnostics, item =>
                item.Code == "BreakingChangeAcknowledgementRequired");

            var published = await registry.ExecuteAsync(
                new PublishContractVersionCommand(
                    updated.Version.ContractVersionId,
                    updated.Version.ConcurrencyToken,
                    true,
                    "operator-a",
                    "Approve breaking proposal version publish",
                    "publish-second"));
            Assert.True(published.IsSuccess,
                DiagnosticText(published.Diagnostics));
            var warmPublished = await registry.ResolveExactAsync("proposal/2");
            Assert.Equal(
                ContractVersionLifecycle.Published,
                warmPublished.ContractVersion!.Version.Lifecycle);
            var retired = await registry.ExecuteAsync(
                new RetireContractVersionCommand(
                    published.Version!.ContractVersionId,
                    published.Version.ConcurrencyToken,
                    "operator-a",
                    "Retire breaking proposal version",
                    "retire-second"));
            Assert.True(retired.IsSuccess,
                DiagnosticText(retired.Diagnostics));

            Assert.DoesNotContain(
                await registry.ListPublishedAsync(),
                item => item.Reference == "proposal/2");
            var historical = await registry.ResolveExactAsync("proposal/2");
            Assert.True(historical.IsSuccess,
                DiagnosticText(historical.Diagnostics));
            Assert.Equal(
                ContractVersionLifecycle.Retired,
                historical.ContractVersion!.Version.Lifecycle);

            await using var connection =
                await dataSource.OpenConnectionAsync();
            Assert.Equal(6, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM contract_audit;"));
        });
    }

    [Fact]
    public async Task Cache_miss_outage_fails_closed_without_source_fallback()
    {
        var time = new MutableTimeProvider(
            DateTimeOffset.Parse("2026-11-02T00:00:00Z"));
        var source = new FailingSource(ResolvedFixture());
        var registry = new VersionedContractRegistryModule(
            source,
            new ContractRegistryCachePolicy(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5)),
            time);

        var warm = await registry.ResolveExactAsync("proposal/1");
        Assert.True(warm.IsSuccess);
        source.Fail = true;
        Assert.True((await registry.ResolveExactAsync("proposal/1")).IsSuccess);

        time.Advance(TimeSpan.FromSeconds(6));
        var expired = await registry.ResolveExactAsync("proposal/1");

        Assert.False(expired.IsSuccess);
        Assert.Equal(
            "ContractRegistryUnavailable",
            Assert.Single(expired.Diagnostics).Code);
        Assert.Equal(2, source.ResolveCalls);
    }

    [Fact]
    public async Task Cached_exact_resolution_p95_is_bounded_and_uses_one_source_call()
    {
        var source = new FailingSource(ResolvedFixture());
        var registry = new VersionedContractRegistryModule(source);
        Assert.True((await registry.ResolveExactAsync("proposal/1")).IsSuccess);
        var durations = new List<double>();

        for (var attempt = 0; attempt < 64; attempt++)
        {
            var started = Stopwatch.GetTimestamp();
            Assert.True((await registry.ResolveExactAsync("proposal/1"))
                .IsSuccess);
            durations.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        var p95 = durations.Order().ElementAt(60);
        Assert.True(p95 <= 25, $"Cached exact p95 was {p95:F3} ms.");
        Assert.Equal(1, source.ResolveCalls);
    }

    [Fact]
    public async Task Bundled_registry_is_read_only()
    {
        var registry = new VersionedContractRegistryModule(
            new BundledVersionedContractRegistry([
                new BundledContractDefinition(
                    "proposal",
                    "Proposal",
                    "1",
                    Schema,
                    DateTimeOffset.Parse("2026-11-02T00:00:00Z"))
            ]));

        var result = await registry.ExecuteAsync(Create("2", Schema));

        Assert.False(result.IsSuccess);
        Assert.Equal("RegistryReadOnly", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Invalid_validation_and_stale_update_have_no_lifecycle_side_effect()
    {
        await WithRegistryAsync(async dataSource =>
        {
            var registry = new VersionedContractRegistryModule(
                new PostgreSqlVersionedContractRegistry(dataSource));
            var created = await registry.ExecuteAsync(Create("1", "{{"));
            Assert.True(created.IsSuccess,
                DiagnosticText(created.Diagnostics));

            var validation = await registry.ValidateAsync(
                created.Version!.ContractVersionId);
            Assert.False(validation.IsValid);
            Assert.Contains(validation.Diagnostics, item =>
                item.Code == "InvalidJson");
            var stale = await registry.ExecuteAsync(
                new UpdateContractDraftCommand(
                    created.Version.ContractVersionId,
                    created.Version.ConcurrencyToken + 1,
                    Schema,
                    "operator-a",
                    "Attempt update with stale contract token",
                    "stale-update"));
            Assert.False(stale.IsSuccess);
            Assert.Contains(stale.Diagnostics, item =>
                item.Code == "ContractConcurrencyConflict");

            await using var connection =
                await dataSource.OpenConnectionAsync();
            Assert.Equal("Draft", await connection.ExecuteScalarAsync<string>(
                "SELECT lifecycle FROM contract_versions WHERE id = @Id;",
                new { Id = created.Version.ContractVersionId }));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM contract_audit;"));
        });
    }

    [Fact]
    public void Compatibility_detects_narrowing_and_accepts_widening()
    {
        var compiler = new ContractSchemaModule();
        var baseline = compiler.Compile("""
            {"type":"object","additionalProperties":false,
             "properties":{"name":{"type":"string","maxLength":120}}}
            """);
        var narrower = compiler.Compile("""
            {"type":"object","additionalProperties":false,
             "properties":{"name":{"type":"string","maxLength":80}}}
            """);
        var wider = compiler.Compile("""
            {"type":"object","additionalProperties":false,
             "properties":{"name":{"type":"string","maxLength":200},
                           "note":{"type":"string"}}}
            """);

        Assert.Equal(
            ContractCompatibility.Breaking,
            ContractCompatibilityAnalyzer.Compare(
                baseline.PathCatalog,
                narrower.PathCatalog));
        Assert.Equal(
            ContractCompatibility.NonBreaking,
            ContractCompatibilityAnalyzer.Compare(
                baseline.PathCatalog,
                wider.PathCatalog));
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Sensitive_schema_is_rejected_without_audit_side_effect()
    {
        await WithRegistryAsync(async dataSource =>
        {
            var registry = new VersionedContractRegistryModule(
                new PostgreSqlVersionedContractRegistry(dataSource));
            var result = await registry.ExecuteAsync(Create(
                "1",
                "{\"clientSecret\":\"real-secret-value\"}"));

            Assert.False(result.IsSuccess);
            Assert.Equal(
                "SensitiveSchemaContent",
                Assert.Single(result.Diagnostics).Code);
            await using var connection =
                await dataSource.OpenConnectionAsync();
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM contract_audit;"));
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Published_version_attached_to_active_profile_cannot_retire()
    {
        await WithRegistryAsync(async dataSource =>
        {
            var registry = new VersionedContractRegistryModule(
                new PostgreSqlVersionedContractRegistry(dataSource));
            var draft = await registry.ExecuteAsync(Create("1", Schema));
            var published = await registry.ExecuteAsync(
                new PublishContractVersionCommand(
                    draft.Version!.ContractVersionId,
                    draft.Version.ConcurrencyToken,
                    false,
                    "operator-a",
                    "Publish proposal for profile attachment",
                    "publish-in-use-proposal"));
            Assert.True(published.IsSuccess);

            await using var connection =
                await dataSource.OpenConnectionAsync();
            var profileId = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO api_destination_profiles (
                    profile_key, display_name, created_by, updated_by)
                VALUES ('in-use', 'In Use', 'test', 'test')
                RETURNING id;
                """);
            var profileVersionId = await connection.ExecuteScalarAsync<long>("""
                INSERT INTO api_destination_profile_versions (
                    profile_id, version_no, lifecycle, environment,
                    schema_base_url, schema_path_template,
                    delivery_base_url, delivery_path_template, method,
                    auth_scheme, network_policy_reference, timeout_seconds,
                    idempotency_header_name, correlation_header_name,
                    allowed_header_names, content_hash,
                    validated_at, validated_content_hash,
                    created_by, updated_by)
                VALUES (
                    @ProfileId, 1, 'Draft', 'Sandbox',
                    'https://contracts.example.test',
                    '/schemas/{contractReference}',
                    'https://delivery.example.test',
                    '/deliveries/{contractVersion}', 'POST', 'None',
                    'test-egress', 10, 'Idempotency-Key',
                    'X-Correlation-Id',
                    ARRAY['Idempotency-Key', 'X-Correlation-Id'],
                    @Hash, now(), @Hash, 'test', 'test')
                RETURNING id;
                """, new
            {
                ProfileId = profileId,
                Hash = published.Version!.SchemaHash
            });
            await connection.ExecuteAsync("""
                INSERT INTO api_destination_profile_contracts (
                    profile_version_id, ordinal, contract_id,
                    contract_version, contract_reference)
                VALUES (@VersionId, 1, 'proposal', '1', 'proposal/1');
                UPDATE api_destination_profile_versions
                SET lifecycle = 'Active', activated_at = now(),
                    activated_by = 'test'
                WHERE id = @VersionId;
                UPDATE api_destination_profiles
                SET active_version_id = @VersionId
                WHERE id = @ProfileId;
                """, new { VersionId = profileVersionId, ProfileId = profileId });
            var auditBefore = await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM contract_audit;");

            var retired = await registry.ExecuteAsync(
                new RetireContractVersionCommand(
                    published.Version.ContractVersionId,
                    published.Version.ConcurrencyToken,
                    "operator-a",
                    "Attempt to retire attached proposal",
                    "retire-in-use-proposal"));

            Assert.False(retired.IsSuccess);
            Assert.Equal(
                "ContractVersionInUse",
                Assert.Single(retired.Diagnostics).Code);
            Assert.Equal(auditBefore,
                await connection.ExecuteScalarAsync<int>(
                    "SELECT count(*) FROM contract_audit;"));
        });
    }

    private static CreateContractDraftCommand Create(
        string version,
        string schema) => new(
        "proposal",
        "Proposal",
        version,
        schema,
        "operator-a",
        "Create proposal contract draft",
        $"create-proposal-{version}");

    private static ResolvedContractVersion ResolvedFixture()
    {
        var compilation = new ContractSchemaModule().Compile(Schema);
        var details = new ContractVersionDetails(
            1,
            1,
            "proposal",
            "Proposal",
            "1",
            "proposal/1",
            ContractVersionLifecycle.Published,
            Schema,
            compilation.CanonicalSchema,
            compilation.SchemaHash,
            ContractCompatibility.Initial,
            null,
            1,
            DateTimeOffset.Parse("2026-11-02T00:00:00Z"),
            DateTimeOffset.Parse("2026-11-02T00:00:00Z"),
            null);
        return new ResolvedContractVersion(
            details,
            compilation.TargetSchema!,
            compilation.PathCatalog);
    }

    private static string DiagnosticText(
        IReadOnlyList<ContractDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(item =>
            $"{item.Path}: {item.Code}: {item.Message}"));

    private static async Task WithRegistryAsync(
        Func<NpgsqlDataSource, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }
        var schema = $"contract_module_{Guid.NewGuid():N}"[..40];
        var quotedSchema = SqlIdentifier.Quote(schema);
        await using var adminDataSource = NpgsqlDataSource.Create(
            connectionString);
        await using var admin = await adminDataSource.OpenConnectionAsync();
        try
        {
            await admin.ExecuteAsync($"CREATE SCHEMA {quotedSchema};");
            var scoped = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            await using var dataSource = NpgsqlDataSource.Create(scoped);
            await using var setup = await dataSource.OpenConnectionAsync();
            await setup.ExecuteAsync("""
                CREATE TABLE mapping_config_versions (id bigint PRIMARY KEY);
                CREATE TABLE delivery_jobs (id bigint PRIMARY KEY);
                """);
            await setup.ExecuteAsync(await ReadMigrationAsync(
                "0014_api_destination_profile_catalog.sql"));
            await setup.ExecuteAsync(await ReadMigrationAsync(
                "0016_versioned_contract_registry.sql"));
            await setup.ExecuteAsync(await ReadMigrationAsync(
                "0017_contract_registry_cutover.sql"));
            await test(dataSource);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
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

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class FailingSource(ResolvedContractVersion fixture) :
        IVersionedContractRegistrySource
    {
        public bool Fail { get; set; }
        public int ResolveCalls { get; private set; }

        public ValueTask<IReadOnlyList<ContractVersionSummary>> ListAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Catalog unavailable.");

        public ValueTask<IReadOnlyList<ContractVersionSummary>>
            ListPublishedAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Catalog unavailable.");

        public ValueTask<ContractAggregateResult> GetAsync(
            string contractKey,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Catalog unavailable.");

        public ValueTask<ContractVersionResult> ResolveExactAsync(
            string reference,
            CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            return Fail
                ? throw new InvalidOperationException("Catalog unavailable.")
                : ValueTask.FromResult(
                    new ContractVersionResult(fixture, []));
        }

        public ValueTask<ContractValidationResult> ValidateAsync(
            long contractVersionId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Catalog unavailable.");

        public ValueTask<ContractMutationResult> ExecuteAsync(
            ContractRegistryCommand command,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Catalog unavailable.");
    }
}
