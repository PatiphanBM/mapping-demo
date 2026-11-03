using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Options;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Tables;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MappingDemo.Tests.MappingConfigs;

public sealed class Phase25CompatibilityBaselineTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Existing_destination_contracts_match_the_golden_responses()
    {
        using var fixture = LoadFixture();
        var root = fixture.RootElement;
        var oldRequest = root
            .GetProperty("legacyMissingDestination")
            .GetProperty("createConfigRequest");
        var request = JsonSerializer.Deserialize<CreateMappingConfigRequest>(
            oldRequest.GetRawText(),
            JsonOptions)!;

        var normalized = DestinationContractNormalizer.Normalize(
            request.Destination,
            request.NormalizedTableId);

        Assert.True(normalized.IsSuccess);
        AssertJsonEqual(
            root.GetProperty("legacyMissingDestination")
                .GetProperty("expectedDestination"),
            normalized.Destination);

        AssertDestinationRoundTrip(
            root.GetProperty("explicitLegacyDestination"));
        AssertDestinationRoundTrip(
            root.GetProperty("apiContractDestination"));

        var versionRequest = JsonSerializer.Deserialize<
            CreateMappingConfigVersionRequest>(
                root.GetProperty("legacyVersionRequest").GetRawText(),
                JsonOptions)!;
        var response = new MappingConfigVersionResponse(
            41,
            7,
            1,
            versionRequest.InputLayout,
            versionRequest.TransformationProgram,
            normalized.Destination!,
            versionRequest.AcceptedExtensions);

        AssertJsonEqual(root.GetProperty("goldenVersionResponse"), response);

        var apiVersionRequest = JsonSerializer.Deserialize<
            CreateMappingConfigVersionRequest>(
                root.GetProperty("apiVersionRequest").GetRawText(),
                JsonOptions)!;
        var apiResponse = new MappingConfigVersionResponse(
            51,
            8,
            3,
            apiVersionRequest.InputLayout,
            apiVersionRequest.TransformationProgram,
            apiVersionRequest.Destination!,
            apiVersionRequest.AcceptedExtensions,
            new ContractSnapshotResponse(
                "{\"type\":\"object\"}",
                "sha256:phase25-baseline",
                new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero),
                [],
                []));

        AssertJsonEqual(
            root.GetProperty("goldenApiVersionResponse"),
            apiResponse);
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Clone_activation_and_intake_keep_versions_pinned()
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

        using var fixture = LoadFixture();
        var root = fixture.RootElement;
        var schema = $"phase25_compat_{Guid.NewGuid():N}"[..32];
        var quotedSchema = SqlIdentifier.Quote(schema);
        var contentRoot = Path.Combine(
            Path.GetTempPath(),
            $"mapping-demo-phase25-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
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
            await using var connection =
                await dataSource.OpenConnectionAsync();
            await connection.ExecuteAsync("""
                INSERT INTO table_definitions (name, kind)
                VALUES ('phase25_source', 'Source'),
                       ('phase25_normalized', 'Normalized');
                """);

            var environment = new TestHostEnvironment
            {
                ContentRootPath = contentRoot
            };
            var pathOptions = Options.Create(new PathOptions
            {
                InputRoot = "input"
            });
            var service = new MappingConfigService(
                dataSource,
                pathOptions,
                environment);
            var createConfig = JsonSerializer.Deserialize<
                CreateMappingConfigRequest>(
                    root.GetProperty("legacyMissingDestination")
                        .GetProperty("createConfigRequest")
                        .GetRawText(),
                    JsonOptions)!;
            var configId = await service.CreateAsync(createConfig);
            var versionRequest = JsonSerializer.Deserialize<
                CreateMappingConfigVersionRequest>(
                    root.GetProperty("legacyVersionRequest").GetRawText(),
                    JsonOptions)!;

            var original = await service.CreateVersionAsync(
                configId,
                versionRequest);
            var explicitDestination = JsonSerializer.Deserialize<
                DestinationDefinition>(
                    root.GetProperty("explicitLegacyDestination")
                        .GetRawText(),
                    JsonOptions)!;
            var clone = await service.CreateVersionAsync(
                configId,
                new CreateMappingConfigVersionRequest(
                    original.InputLayout,
                    original.TransformationProgram,
                    explicitDestination,
                    original.AcceptedExtensions));

            Assert.Equal(1, original.VersionNo);
            Assert.Equal(2, clone.VersionNo);
            AssertJsonEqual(
                root.GetProperty("legacyMissingDestination")
                    .GetProperty("expectedDestination"),
                original.Destination);
            AssertJsonEqual(
                root.GetProperty("explicitLegacyDestination"),
                clone.Destination);

            var activeConfigs = new ActiveConfigProvider(
                dataSource,
                pathOptions,
                environment);
            var intake = new FileIntake(dataSource);
            var fileJobs = new FileJobService(dataSource);

            await service.ActivateVersionAsync(configId, original.Id);
            var originalActive = Assert.Single(
                await activeConfigs.GetAllAsync());
            Assert.Equal(original.Id, originalActive.ActiveVersionId);
            var originalJobId = await intake.CreateJobAsync(
                configId,
                originalActive.ActiveVersionId!.Value,
                Intake("original.csv", "phase25-original"));

            await service.ActivateVersionAsync(configId, clone.Id);
            var cloneActive = Assert.Single(
                await activeConfigs.GetAllAsync());
            Assert.Equal(clone.Id, cloneActive.ActiveVersionId);
            var cloneJobId = await intake.CreateJobAsync(
                configId,
                cloneActive.ActiveVersionId!.Value,
                Intake("clone.csv", "phase25-clone"));

            Assert.NotNull(originalJobId);
            Assert.NotNull(cloneJobId);
            Assert.Equal(
                original.Id,
                (await fileJobs.GetByIdAsync(originalJobId.Value))!
                    .ConfigVersionId);
            Assert.Equal(
                clone.Id,
                (await fileJobs.GetByIdAsync(cloneJobId.Value))!
                    .ConfigVersionId);

            var details = await service.GetDetailsByIdAsync(configId);
            Assert.NotNull(details);
            Assert.Collection(
                details.Versions,
                version =>
                {
                    Assert.False(version.IsActive);
                    Assert.Equal(original.Id, version.Id);
                },
                version =>
                {
                    Assert.True(version.IsActive);
                    Assert.Equal(clone.Id, version.Id);
                });

            var persistedVersions = (await connection.QueryAsync<
                PersistedVersion>("""
                    SELECT
                        version_no AS "VersionNo",
                        input_layout::text AS "InputLayout",
                        transformation_program::text
                            AS "TransformationProgram",
                        destination_definition::text AS "Destination"
                    FROM mapping_config_versions
                    WHERE config_id = @ConfigId
                    ORDER BY version_no;
                    """, new { ConfigId = configId })).ToArray();
            Assert.Equal(2, persistedVersions.Length);
            foreach (var persisted in persistedVersions)
            {
                AssertJsonEqual(
                    root.GetProperty("legacyVersionRequest")
                        .GetProperty("inputLayout"),
                    JsonNode.Parse(persisted.InputLayout));
                AssertJsonEqual(
                    root.GetProperty("legacyVersionRequest")
                        .GetProperty("transformationProgram"),
                    JsonNode.Parse(persisted.TransformationProgram));
                AssertJsonEqual(
                    root.GetProperty("explicitLegacyDestination"),
                    JsonNode.Parse(persisted.Destination));
            }
            Assert.Equal(
                2,
                await connection.ExecuteScalarAsync<int>("""
                    SELECT COUNT(*)::integer
                    FROM outbox
                    WHERE topic = 'mapping.file-import';
                    """));
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static void AssertDestinationRoundTrip(JsonElement expected)
    {
        var destination = JsonSerializer.Deserialize<DestinationDefinition>(
            expected.GetRawText(),
            JsonOptions);

        Assert.NotNull(destination);
        AssertJsonEqual(expected, destination);
    }

    private static void AssertJsonEqual(JsonElement expected, object? actual)
    {
        var expectedNode = JsonNode.Parse(expected.GetRawText());
        var actualNode = actual as JsonNode
            ?? JsonSerializer.SerializeToNode(actual, JsonOptions);

        Assert.True(
            JsonNode.DeepEquals(expectedNode, actualNode),
            $"Expected: {expectedNode}{Environment.NewLine}Actual: {actualNode}");
    }

    private static FileIntakeDetails Intake(
        string fileName,
        string intakeKey) => new(
            $"input/phase25/orders/{fileName}",
            fileName,
            10,
            new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc),
            intakeKey);

    private static JsonDocument LoadFixture()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Phase25",
            "compatibility-baseline.golden.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private sealed record PersistedVersion(
        int VersionNo,
        string InputLayout,
        string TransformationProgram,
        string Destination);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "MappingDemo.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
