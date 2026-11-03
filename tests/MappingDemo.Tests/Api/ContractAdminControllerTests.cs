using System.Security.Claims;
using System.Text.Json;
using MappingDemo.Api;
using MappingDemo.Api.Contracts.Contracts;
using MappingDemo.Api.Controllers;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Transformation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MappingDemo.Tests.Api;

public sealed class ContractAdminControllerTests
{
    private const string Schema = """
        {"type":"object","additionalProperties":false,
         "properties":{"name":{"type":"string"}}}
        """;

    [Fact]
    public void OpenApi_create_contract_has_no_authoritative_or_secret_fields()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSwaggerGen(OpenApiConfiguration.Configure);
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<ISchemaGenerator>();
        var repository = new SchemaRepository();

        _ = generator.GenerateSchema(
            typeof(CreateContractDraftRequest),
            repository);
        var schema = repository.Schemas["CreateContractDraftRequest"];
        Assert.NotNull(schema.Properties);
        var properties = schema.Properties!;

        Assert.Equal(
            ["contractKey", "displayName", "reason", "schema", "version"],
            properties.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("schemaHash", properties.Keys);
        Assert.DoesNotContain("canonicalSchema", properties.Keys);
        Assert.DoesNotContain("privateKey", properties.Keys);
        Assert.DoesNotContain("clientSecret", properties.Keys);
    }

    [Fact]
    public async Task Unauthorized_mutation_is_rejected_without_execution()
    {
        var registry = new FakeRegistry();
        var controller = Controller(registry, publishingEnabled: true);

        var result = await controller.Create(
            new CreateContractDraftRequest(
                "proposal",
                "Proposal",
                "1",
                Schema,
                "Create initial proposal contract"),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(registry.Commands);
    }

    [Fact]
    public async Task Write_permission_cannot_publish_and_gate_can_freeze_publish()
    {
        var registry = new FakeRegistry();
        var controller = Controller(registry, publishingEnabled: true);
        controller.HttpContext.User = Authenticated("ContractAdmin.WriteDraft");
        controller.Request.Headers.IfMatch = "\"1\"";

        var forbidden = await controller.Publish(
            1,
            new PublishContractVersionRequest(
                false,
                "Publish approved contract version"),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(forbidden);
        Assert.Empty(registry.Commands);

        controller = Controller(registry, publishingEnabled: false);
        controller.HttpContext.User = Authenticated("ContractAdmin.Publish");
        controller.Request.Headers.IfMatch = "\"1\"";
        var frozen = await controller.Publish(
            1,
            new PublishContractVersionRequest(
                false,
                "Publish approved contract version"),
            CancellationToken.None);
        Assert.IsType<ForbidResult>(frozen);
        Assert.Empty(registry.Commands);
    }

    [Fact]
    public async Task Stale_update_returns_precondition_failed_and_preserves_schema()
    {
        var registry = new FakeRegistry { RejectConcurrency = true };
        var controller = Controller(registry, publishingEnabled: true);
        controller.HttpContext.User = Authenticated("ContractAdmin.WriteDraft");
        controller.Request.Headers.IfMatch = "\"9\"";

        var result = await controller.UpdateDraft(
            1,
            new UpdateContractDraftRequest(
                "{\"type\":\"array\"}",
                "Attempt stale schema update"),
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status412PreconditionFailed,
            Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(Schema, registry.Version.DraftSchema);
    }

    [Fact]
    public async Task Validate_is_side_effect_free_and_response_has_no_internal_values()
    {
        var registry = new FakeRegistry();
        var controller = Controller(registry, publishingEnabled: true);
        controller.HttpContext.User = Authenticated("ContractAdmin.WriteDraft");

        var result = await controller.Validate(1, CancellationToken.None);

        var response = Assert.IsType<ContractValidationResponse>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(response.IsValid);
        Assert.Empty(registry.Commands);
        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("Host=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ContractFixtures", json, StringComparison.Ordinal);
        Assert.DoesNotContain("privateKey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Publish_requires_if_match_and_forwards_acknowledgement()
    {
        var registry = new FakeRegistry();
        var controller = Controller(registry, publishingEnabled: true);
        controller.HttpContext.User = Authenticated("ContractAdmin.Publish");

        var missing = await controller.Publish(
            1,
            new PublishContractVersionRequest(
                true,
                "Approve breaking contract publication"),
            CancellationToken.None);
        Assert.Equal(
            StatusCodes.Status428PreconditionRequired,
            Assert.IsType<ObjectResult>(missing).StatusCode);

        controller.Request.Headers.IfMatch = "\"1\"";
        var published = await controller.Publish(
            1,
            new PublishContractVersionRequest(
                true,
                "Approve breaking contract publication"),
            CancellationToken.None);

        Assert.IsType<ObjectResult>(published);
        var command = Assert.IsType<PublishContractVersionCommand>(
            Assert.Single(registry.Commands));
        Assert.True(command.AcknowledgeBreakingChange);
        Assert.Equal(1, command.ExpectedConcurrencyToken);
    }

    private static ContractAdminController Controller(
        FakeRegistry registry,
        bool publishingEnabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ContractRegistry:PublishingEnabled"] =
                    publishingEnabled.ToString(),
                ["ContractRegistry:AllowLocalOperatorHeader"] = "false"
            })
            .Build();
        return new ContractAdminController(
            registry,
            new ContractRegistryAuthorizer(configuration))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
    }

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

    private sealed class FakeRegistry : IVersionedContractRegistry
    {
        private readonly ContractSchemaCompilationResult _compilation =
            new ContractSchemaModule().Compile(Schema);

        public bool RejectConcurrency { get; init; }
        public List<ContractRegistryCommand> Commands { get; } = [];
        public ContractVersionDetails Version { get; private set; } = new(
            1,
            1,
            "proposal",
            "Proposal",
            "1",
            "proposal/1",
            ContractVersionLifecycle.Draft,
            Schema,
            null,
            null,
            null,
            null,
            1,
            DateTimeOffset.Parse("2026-11-02T00:00:00Z"),
            null,
            null);

        public ValueTask<IReadOnlyList<ContractVersionSummary>> ListAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<ContractVersionSummary>>([]);

        public ValueTask<IReadOnlyList<ContractVersionSummary>>
            ListPublishedAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<ContractVersionSummary>>([]);

        public ValueTask<ContractAggregateResult> GetAsync(
            string contractKey,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ContractAggregateResult(
                new ContractAggregate(
                    1,
                    "proposal",
                    "Proposal",
                    1,
                    [Version]),
                []));

        public ValueTask<ContractVersionResult> ResolveExactAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ContractVersionResult(
                null,
                [new ContractDiagnostic(
                    "contract",
                    "ContractNotPublished",
                    "Draft is not published.")]));

        public ValueTask<ContractValidationResult> ValidateAsync(
            long contractVersionId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ContractValidationResult(
                true,
                _compilation.CanonicalSchema,
                _compilation.SchemaHash,
                _compilation.TargetSchema,
                _compilation.PathCatalog,
                ContractCompatibility.Initial,
                null,
                []));

        public ValueTask<ContractMutationResult> ExecuteAsync(
            ContractRegistryCommand command,
            CancellationToken cancellationToken = default)
        {
            if (RejectConcurrency)
            {
                return ValueTask.FromResult(
                    new ContractMutationResult(
                        null,
                        [new ContractDiagnostic(
                            "contract",
                            "ContractConcurrencyConflict",
                            "The Draft has changed.")]));
            }
            Commands.Add(command);
            if (command is UpdateContractDraftCommand update)
            {
                Version = Version with
                {
                    DraftSchema = update.Schema,
                    ConcurrencyToken = Version.ConcurrencyToken + 1
                };
            }
            return ValueTask.FromResult(new ContractMutationResult(
                Version,
                []));
        }
    }
}
