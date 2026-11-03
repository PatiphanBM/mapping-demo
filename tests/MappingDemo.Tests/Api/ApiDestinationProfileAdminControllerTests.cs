using System.Security.Claims;
using Dapper;
using MappingDemo.Api.Contracts.ApiDestinationProfiles;
using MappingDemo.Api.Controllers;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MappingDemo.Tests.Api;

public sealed class ApiDestinationProfileAdminControllerTests
{
    [Fact]
    public void Compare_mode_freezes_profile_mutation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiDestinationProfileCatalog:SourceMode"] = "Compare"
            })
            .Build();
        var authorizer = new ProfileAdministrationAuthorizer(configuration);

        Assert.False(authorizer.IsAuthorized(
            Authenticated("ProfileAdmin.Write"),
            "ProfileAdmin.Write",
            "operator-a",
            mutation: true));
        Assert.True(authorizer.IsAuthorized(
            Authenticated("ProfileAdmin.Read"),
            "ProfileAdmin.Read",
            null,
            mutation: false));
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Unauthenticated_mutation_returns_unauthorized()
    {
        await WithControllerAsync(async (controller, _) =>
        {
            var result = await controller.Create(
                new CreateApiDestinationProfileRequest(
                    "partner-a",
                    "Partner A",
                    Draft(),
                    "Create approved sandbox profile"),
                CancellationToken.None);

            Assert.IsType<UnauthorizedResult>(result);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Permissions_projection_hides_actions_not_granted()
    {
        await WithControllerAsync((controller, _) =>
        {
            controller.HttpContext.User = Authenticated("ProfileAdmin.Read");

            var response = Assert.IsType<
                ApiDestinationProfileAdminCapabilitiesResponse>(
                Assert.IsType<OkObjectResult>(
                    controller.GetPermissions()).Value);

            Assert.False(response.CanReadEndpoints);
            Assert.False(response.CanWriteDraft);
            Assert.False(response.CanActivate);
            Assert.False(response.CanActivateProduction);
            Assert.False(response.CanDisable);
            return Task.CompletedTask;
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task List_is_bounded_and_supports_offset_pagination()
    {
        await WithControllerAsync(async (controller, _) =>
        {
            controller.HttpContext.User = Authenticated(
                "ProfileAdmin.WriteDraft",
                "ProfileAdmin.Read");
            foreach (var suffix in new[] { "a", "b", "c" })
            {
                controller.Request.Headers["X-Request-Id"] =
                    $"pagination-create-{suffix}";
                var result = await controller.Create(
                    new CreateApiDestinationProfileRequest(
                        $"partner-{suffix}",
                        $"Partner {suffix.ToUpperInvariant()}",
                        Draft(),
                        "Create profile for pagination test"),
                    CancellationToken.None);
                Assert.IsType<CreatedAtActionResult>(result);
            }

            var response = Assert.IsType<OkObjectResult>(await controller.GetAll(
                CancellationToken.None,
                limit: 2,
                offset: 1));
            var profiles = Assert.IsAssignableFrom<IEnumerable<
                ApiDestinationProfileAdminResponse>>(response.Value).ToArray();

            Assert.Equal(["partner-b", "partner-c"],
                profiles.Select(profile => profile.ProfileKey));
            Assert.All(profiles, profile => Assert.Single(profile.Versions));
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Read_without_endpoint_permission_returns_redacted_projection()
    {
        await WithControllerAsync(async (controller, _) =>
        {
            controller.HttpContext.User = Authenticated(
                "ProfileAdmin.WriteDraft",
                "ProfileAdmin.Read");
            var created = await controller.Create(
                new CreateApiDestinationProfileRequest(
                    "partner-a",
                    "Partner A",
                    Draft() with
                    {
                        AuthScheme = "Mtls",
                        AuthSecretReference = "partner-a-mtls"
                    },
                    "Create approved sandbox profile"),
                CancellationToken.None);
            Assert.IsType<CreatedAtActionResult>(created);

            controller.HttpContext.User = Authenticated("ProfileAdmin.Read");
            var result = await controller.Get(
                "partner-a",
                CancellationToken.None);

            var response = Assert.IsType<
                ApiDestinationProfileAdminResponse>(
                Assert.IsType<OkObjectResult>(result).Value);
            var version = Assert.Single(response.Versions);
            Assert.Null(version.SchemaBaseUrl);
            Assert.Null(version.DeliveryBaseUrl);
            Assert.Null(version.AuthSecretReference);
            Assert.Equal("POST", version.Method);
            Assert.Equal(
                "/deliveries/{contractVersion}",
                version.DeliveryPathTemplate);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Validate_reports_missing_secret_reference_without_returning_a_value()
    {
        await WithControllerAsync(async (controller, _) =>
        {
            controller.HttpContext.User = Authenticated(
                "ProfileAdmin.WriteDraft",
                "ProfileAdmin.Read");
            var created = Assert.IsType<CreatedAtActionResult>(
                await controller.Create(
                    new CreateApiDestinationProfileRequest(
                        "partner-secret",
                        "Partner Secret",
                        Draft() with
                        {
                            AuthScheme = "Mtls",
                            AuthSecretReference = "definitely-missing-secret"
                        },
                        "Create secret validation draft"),
                    CancellationToken.None));
            var profile = Assert.IsType<ApiDestinationProfileAdminResponse>(
                created.Value);

            var result = await controller.Validate(
                profile.ProfileKey,
                1,
                new ApiDestinationProfileActionRequest(
                    "Check referenced secret exists"),
                CancellationToken.None);

            var validation = Assert.IsType<
                ApiDestinationProfileValidationResponse>(
                Assert.IsType<OkObjectResult>(result).Value);
            Assert.False(validation.IsValid);
            Assert.Equal("Missing", validation.SecretStatus);
            Assert.Contains(validation.Diagnostics, diagnostic =>
                diagnostic.Code == "MissingAuthSecretReference");
            Assert.DoesNotContain(
                "definitely-missing-secret",
                System.Text.Json.JsonSerializer.Serialize(validation));
            var unchanged = Assert.IsType<ApiDestinationProfileAdminResponse>(
                Assert.IsType<OkObjectResult>(await controller.Get(
                    profile.ProfileKey,
                    CancellationToken.None)).Value);
            Assert.Null(Assert.Single(unchanged.Versions).ValidatedAt);
            Assert.Equal(1, Assert.Single(unchanged.Versions).ConcurrencyToken);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Lifecycle_mutations_are_audited_and_disable_keeps_history()
    {
        await WithControllerAsync(async (controller, dataSource) =>
        {
            controller.HttpContext.User = Authenticated(
                "ProfileAdmin.WriteDraft",
                "ProfileAdmin.Activate",
                "ProfileAdmin.Disable",
                "ProfileAdmin.Read",
                "ProfileAdmin.EndpointRead");
            controller.Request.Headers["X-Operator-Id"] = "spoofed-operator";
            controller.Request.Headers["X-Request-Id"] = "audit-create";
            var created = Assert.IsType<ApiDestinationProfileAdminResponse>(
                Assert.IsType<CreatedAtActionResult>(await controller.Create(
                    new CreateApiDestinationProfileRequest(
                        "partner-audit",
                        "Partner Audit",
                        Draft(),
                        "Create audited profile draft"),
                    CancellationToken.None)).Value);

            controller.Request.Headers["X-Request-Id"] = "audit-validate";
            var validated = Assert.IsType<ApiDestinationProfileValidationResponse>(
                Assert.IsType<OkObjectResult>(await controller.Validate(
                    created.ProfileKey,
                    1,
                    new ApiDestinationProfileActionRequest(
                        "Validate audited profile draft"),
                    CancellationToken.None)).Value);
            Assert.True(validated.IsValid);

            controller.Request.Headers["X-Request-Id"] = "audit-activate";
            controller.Request.Headers.IfMatch =
                $"\"{created.ConcurrencyToken}\"";
            var active = Assert.IsType<ApiDestinationProfileAdminResponse>(
                Assert.IsType<OkObjectResult>(await controller.Activate(
                    created.ProfileKey,
                    1,
                    new ApiDestinationProfileActionRequest(
                        "Activate audited sandbox profile"),
                    CancellationToken.None)).Value);
            Assert.Equal(1, active.ActiveVersionNo);

            controller.Request.Headers["X-Request-Id"] = "audit-disable";
            controller.Request.Headers.IfMatch =
                $"\"{active.ConcurrencyToken}\"";
            var disabled = Assert.IsType<ApiDestinationProfileAdminResponse>(
                Assert.IsType<OkObjectResult>(await controller.Disable(
                    created.ProfileKey,
                    new ApiDestinationProfileActionRequest(
                        "Disable audited sandbox profile"),
                    CancellationToken.None)).Value);
            Assert.Null(disabled.ActiveVersionNo);
            Assert.Equal("Disabled", Assert.Single(disabled.Versions).Lifecycle);

            await using var connection = await dataSource.OpenConnectionAsync();
            var audits = (await connection.QueryAsync<AuditRow>("""
                SELECT action AS "Action", actor AS "Actor",
                       reason AS "Reason", request_id AS "RequestId",
                       before_content_hash AS "BeforeHash",
                       after_content_hash AS "AfterHash",
                       concurrency_token AS "ConcurrencyToken"
                FROM api_destination_profile_audit
                ORDER BY id;
                """)).ToArray();
            Assert.Equal(
                ["CreateProfile", "ValidateDraft", "Activate", "Disable"],
                audits.Select(audit => audit.Action));
            Assert.All(audits, audit =>
            {
                Assert.Equal("operator-a", audit.Actor);
                Assert.False(string.IsNullOrWhiteSpace(audit.Reason));
                Assert.StartsWith("audit-", audit.RequestId);
                Assert.NotNull(audit.AfterHash);
                Assert.True(audit.ConcurrencyToken > 0);
            });
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Concurrent_activate_and_disable_have_one_winner()
    {
        await WithControllerAsync(async (controller, dataSource) =>
        {
            controller.HttpContext.User = Authenticated(
                "ProfileAdmin.WriteDraft",
                "ProfileAdmin.Activate");
            controller.Request.Headers["X-Request-Id"] = "race-create";
            var created = Assert.IsType<ApiDestinationProfileAdminResponse>(
                Assert.IsType<CreatedAtActionResult>(await controller.Create(
                    new CreateApiDestinationProfileRequest(
                        "partner-race",
                        "Partner Race",
                        Draft(),
                        "Create profile for race test"),
                    CancellationToken.None)).Value);
            controller.Request.Headers["X-Request-Id"] = "race-validate-v1";
            _ = await controller.Validate(
                created.ProfileKey,
                1,
                new ApiDestinationProfileActionRequest(
                    "Validate first race version"),
                CancellationToken.None);
            controller.Request.Headers["X-Request-Id"] = "race-activate-v1";
            controller.Request.Headers.IfMatch =
                $"\"{created.ConcurrencyToken}\"";
            var active = Assert.IsType<ApiDestinationProfileAdminResponse>(
                Assert.IsType<OkObjectResult>(await controller.Activate(
                    created.ProfileKey,
                    1,
                    new ApiDestinationProfileActionRequest(
                        "Activate first race version"),
                    CancellationToken.None)).Value);

            controller.Request.Headers["X-Request-Id"] = "race-create-v2";
            controller.Request.Headers.IfMatch =
                $"\"{active.ConcurrencyToken}\"";
            var withDraft = Assert.IsType<ApiDestinationProfileAdminResponse>(
                Assert.IsType<OkObjectResult>(await controller.CreateDraft(
                    created.ProfileKey,
                    null,
                    new ApiDestinationProfileActionRequest(
                        "Create second race version"),
                    CancellationToken.None)).Value);
            controller.Request.Headers["X-Request-Id"] = "race-validate-v2";
            _ = await controller.Validate(
                created.ProfileKey,
                2,
                new ApiDestinationProfileActionRequest(
                    "Validate second race version"),
                CancellationToken.None);

            var activate = NewService(dataSource).ActivateAsync(
                created.ProfileKey,
                2,
                withDraft.ConcurrencyToken,
                "operator-a",
                "Approve concurrent activation",
                "race-activate-v2",
                CancellationToken.None);
            var disable = NewService(dataSource).DisableAsync(
                created.ProfileKey,
                withDraft.ConcurrencyToken,
                "operator-b",
                "Approve concurrent disable",
                "race-disable",
                CancellationToken.None);
            var results = await Task.WhenAll(activate, disable);

            Assert.Single(results.Where(result => result is not null));
            var final = await NewService(dataSource).GetAsync(
                created.ProfileKey,
                CancellationToken.None);
            Assert.NotNull(final);
            Assert.True(final.ActiveVersionNo is null or 2);
            Assert.InRange(final.ConcurrencyToken,
                withDraft.ConcurrencyToken + 1,
                withDraft.ConcurrencyToken + 1);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Production_activation_requires_production_permission()
    {
        await WithControllerAsync(async (controller, _) =>
        {
            controller.HttpContext.User = Authenticated(
                "ProfileAdmin.WriteDraft");
            var created = Assert.IsType<CreatedAtActionResult>(
                await controller.Create(
                    new CreateApiDestinationProfileRequest(
                        "partner-production",
                        "Partner Production",
                        Draft() with { Environment = "Production" },
                        "Create production profile draft"),
                    CancellationToken.None));
            var profile = Assert.IsType<ApiDestinationProfileAdminResponse>(
                created.Value);
            controller.Request.Headers["X-Request-Id"] =
                "validate-production-profile";
            var validationResult = await controller.Validate(
                profile.ProfileKey,
                1,
                new ApiDestinationProfileActionRequest(
                    "Validate production profile draft"),
                CancellationToken.None);
            Assert.IsType<OkObjectResult>(validationResult);

            controller.HttpContext.User = Authenticated(
                "ProfileAdmin.Activate");
            controller.Request.Headers.IfMatch =
                $"\"{profile.ConcurrencyToken}\"";
            var result = await controller.Activate(
                profile.ProfileKey,
                1,
                new ApiDestinationProfileActionRequest(
                    "Approve production activation"),
                CancellationToken.None);

            Assert.IsType<ForbidResult>(result);
        });
    }

    [Fact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Stale_update_returns_current_version_token()
    {
        await WithControllerAsync(async (controller, _) =>
        {
            controller.HttpContext.User = Authenticated(
                "ProfileAdmin.WriteDraft");
            var created = Assert.IsType<CreatedAtActionResult>(
                await controller.Create(
                    new CreateApiDestinationProfileRequest(
                        "partner-stale",
                        "Partner Stale",
                        Draft(),
                        "Create concurrency test draft"),
                    CancellationToken.None));
            var profile = Assert.IsType<ApiDestinationProfileAdminResponse>(
                created.Value);
            var draft = Assert.Single(profile.Versions);
            controller.Request.Headers.IfMatch =
                $"\"{draft.ConcurrencyToken}\"";
            controller.Request.Headers["X-Request-Id"] =
                "first-stale-profile-update";
            var firstUpdate = await controller.UpdateDraft(
                profile.ProfileKey,
                draft.VersionNo,
                new UpdateApiDestinationProfileDraftRequest(
                    Draft() with { TimeoutSeconds = 20 },
                    "First approved draft update"),
                CancellationToken.None);
            Assert.IsType<OkObjectResult>(firstUpdate);

            controller.Request.Headers.IfMatch =
                $"\"{draft.ConcurrencyToken}\"";
            var stale = await controller.UpdateDraft(
                profile.ProfileKey,
                draft.VersionNo,
                new UpdateApiDestinationProfileDraftRequest(
                    Draft() with { TimeoutSeconds = 30 },
                    "Second stale draft update"),
                CancellationToken.None);

            var problem = Assert.IsType<ProblemDetails>(
                Assert.IsType<ObjectResult>(stale).Value);
            Assert.Equal(StatusCodes.Status412PreconditionFailed,
                problem.Status);
            Assert.Equal(draft.ConcurrencyToken + 1,
                problem.Extensions["currentConcurrencyToken"]);
        });
    }

    private static ApiDestinationProfileDraftRequest Draft() => new(
        "Sandbox",
        "https://contracts.example.test",
        "/schemas/{contractReference}",
        "https://delivery.example.test",
        "/deliveries/{contractVersion}",
        "POST",
        "None",
        null,
        "test-egress",
        10,
        "Idempotency-Key",
        "X-Correlation-Id",
        ["Idempotency-Key", "X-Correlation-Id"],
        [new SupportedContract("proposal", "1", "proposal/1")],
        [new PayloadBinding("meta.fileJobId", RuntimeValueSource.FileJobId)]);

    private static ClaimsPrincipal Authenticated(params string[] permissions)
    {
        var identity = new ClaimsIdentity(
            permissions.Select(permission =>
                new Claim("permission", permission)),
            "Test",
            ClaimTypes.Name,
            ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.Name, "operator-a"));
        return new ClaimsPrincipal(identity);
    }

    private static async Task WithControllerAsync(
        Func<ApiDestinationProfileAdminController, NpgsqlDataSource, Task> test)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MAPPING_POSTGRES_INTEGRATION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var schema = $"profile_admin_{Guid.NewGuid():N}"[..40];
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
            await using var setup = await dataSource.OpenConnectionAsync();
            await setup.ExecuteAsync("""
                CREATE TABLE mapping_config_versions (id bigint PRIMARY KEY);
                CREATE TABLE delivery_jobs (id bigint PRIMARY KEY);
                """);
            await setup.ExecuteAsync(await ReadMigrationAsync());

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProfileAdministration:AllowLocalOperatorHeader"] = "false"
                })
                .Build();
            var controller = new ApiDestinationProfileAdminController(
                NewService(dataSource),
                new ProfileAdministrationAuthorizer(configuration))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity())
                    }
                }
            };

            await test(controller, dataSource);
        }
        finally
        {
            await admin.ExecuteAsync(
                $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE;");
        }
    }

    private static async Task<string> ReadMigrationAsync()
    {
        const string resourceName =
            "MappingDemo.Shared.Database.Migrations." +
            "0014_api_destination_profile_catalog.sql";
        await using var stream = typeof(MigrationRunner).Assembly
            .GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }

    private static ApiDestinationProfileAdministrationService NewService(
        NpgsqlDataSource dataSource) => new(
        dataSource,
        new EnvironmentProfileSecretReferenceValidator(),
        new NoOpCacheInvalidator());

    private sealed class NoOpCacheInvalidator :
        IApiDestinationProfileCatalogCacheInvalidator
    {
        public void InvalidateSafeProfiles()
        {
        }
    }

    private sealed record AuditRow(
        string Action,
        string Actor,
        string Reason,
        string RequestId,
        string? BeforeHash,
        string? AfterHash,
        long ConcurrencyToken);
}
