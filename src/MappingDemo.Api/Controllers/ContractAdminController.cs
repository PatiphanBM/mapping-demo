using MappingDemo.Api.Contracts.Contracts;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("admin/contracts")]
public sealed class ContractAdminController(
    IVersionedContractRegistry registry,
    IContractRegistryAuthorizer authorizer) : ControllerBase
{
    private const long MaximumRequestBytes = 1_100_000;

    [HttpGet("permissions")]
    public IActionResult GetPermissions()
    {
        if (!Authorize("ContractAdmin.Read", false, out _))
        {
            return AuthorizationFailure();
        }

        return Ok(new ContractAdminCapabilitiesResponse(
            Can("ContractAdmin.WriteDraft"),
            Can("ContractAdmin.Publish")));
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ContractVersionLifecycle? lifecycle,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ContractAdmin.Read", false, out _))
        {
            return AuthorizationFailure();
        }

        var values = await registry.ListAsync(cancellationToken);
        return Ok(lifecycle is null
            ? values
            : values.Where(item => item.Lifecycle == lifecycle).ToArray());
    }

    [HttpGet("{contractKey}")]
    public async Task<IActionResult> Get(
        string contractKey,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ContractAdmin.Read", false, out _))
        {
            return AuthorizationFailure();
        }

        var result = await registry.GetAsync(contractKey, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result.Diagnostics);
        }

        Response.Headers.ETag = Quote(result.Contract!.ConcurrencyToken);
        return Ok(result.Contract);
    }

    [HttpPost]
    [RequestSizeLimit(MaximumRequestBytes)]
    public async Task<IActionResult> Create(
        CreateContractDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ContractAdmin.WriteDraft", true, out var actor))
        {
            return AuthorizationFailure();
        }

        var result = await registry.ExecuteAsync(
            new CreateContractDraftCommand(
                request.ContractKey,
                request.DisplayName,
                request.Version,
                request.Schema,
                actor!,
                request.Reason,
                RequestId()),
            cancellationToken);
        return MutationResult(result, StatusCodes.Status201Created);
    }

    [HttpPut("versions/{contractVersionId:long}")]
    [RequestSizeLimit(MaximumRequestBytes)]
    public async Task<IActionResult> UpdateDraft(
        long contractVersionId,
        UpdateContractDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ContractAdmin.WriteDraft", true, out var actor))
        {
            return AuthorizationFailure();
        }
        if (!TryToken(out var token, out var error))
        {
            return error!;
        }

        var result = await registry.ExecuteAsync(
            new UpdateContractDraftCommand(
                contractVersionId,
                token,
                request.Schema,
                actor!,
                request.Reason,
                RequestId()),
            cancellationToken);
        return MutationResult(result);
    }

    [HttpPost("versions/{contractVersionId:long}/validate")]
    public async Task<IActionResult> Validate(
        long contractVersionId,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ContractAdmin.WriteDraft", false, out _))
        {
            return AuthorizationFailure();
        }

        var result = await registry.ValidateAsync(
            contractVersionId,
            cancellationToken);
        return result.Diagnostics.Any(item =>
            item.Code == "ContractRegistryUnavailable")
                ? Failure(result.Diagnostics)
                : Ok(new ContractValidationResponse(
                    result.IsValid,
                    result.CanonicalSchema,
                    result.SchemaHash,
                    result.PathCatalog,
                    result.Compatibility,
                    result.CompatibilityBaseVersionId,
                    result.Diagnostics));
    }

    [HttpPost("versions/{contractVersionId:long}/clone")]
    public async Task<IActionResult> Clone(
        long contractVersionId,
        CloneContractVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ContractAdmin.WriteDraft", true, out var actor))
        {
            return AuthorizationFailure();
        }

        var result = await registry.ExecuteAsync(
            new CloneContractVersionCommand(
                contractVersionId,
                request.Version,
                actor!,
                request.Reason,
                RequestId()),
            cancellationToken);
        return MutationResult(result, StatusCodes.Status201Created);
    }

    [HttpPost("versions/{contractVersionId:long}/publish")]
    public async Task<IActionResult> Publish(
        long contractVersionId,
        PublishContractVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ContractAdmin.Publish", true, out var actor))
        {
            return AuthorizationFailure();
        }
        if (!TryToken(out var token, out var error))
        {
            return error!;
        }

        var result = await registry.ExecuteAsync(
            new PublishContractVersionCommand(
                contractVersionId,
                token,
                request.AcknowledgeBreakingChange,
                actor!,
                request.Reason,
                RequestId()),
            cancellationToken);
        return MutationResult(result);
    }

    [HttpPost("versions/{contractVersionId:long}/retire")]
    public async Task<IActionResult> Retire(
        long contractVersionId,
        ContractActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ContractAdmin.Publish", true, out var actor))
        {
            return AuthorizationFailure();
        }
        if (!TryToken(out var token, out var error))
        {
            return error!;
        }

        var result = await registry.ExecuteAsync(
            new RetireContractVersionCommand(
                contractVersionId,
                token,
                actor!,
                request.Reason,
                RequestId()),
            cancellationToken);
        return MutationResult(result);
    }

    private IActionResult MutationResult(
        ContractMutationResult result,
        int successStatus = StatusCodes.Status200OK)
    {
        if (!result.IsSuccess)
        {
            return Failure(result.Diagnostics);
        }

        Response.Headers.ETag = Quote(result.Version!.ConcurrencyToken);
        return StatusCode(successStatus, result.Version);
    }

    private IActionResult Failure(
        IReadOnlyList<ContractDiagnostic> diagnostics)
    {
        var codes = diagnostics.Select(item => item.Code).ToHashSet(
            StringComparer.Ordinal);
        var status = codes.Contains("ContractRegistryUnavailable")
            ? StatusCodes.Status503ServiceUnavailable
            : codes.Contains("ContractNotFound")
                ? StatusCodes.Status404NotFound
                : codes.Contains("ContractConcurrencyConflict")
                    ? StatusCodes.Status412PreconditionFailed
                    : codes.Overlaps([
                        "ContractConflict",
                        "BreakingChangeAcknowledgementRequired",
                        "ContractVersionInUse",
                        "PublishedContractRequired",
                        "RegistryReadOnly"])
                        ? StatusCodes.Status409Conflict
                        : StatusCodes.Status400BadRequest;
        return StatusCode(status, new ProblemDetails
        {
            Status = status,
            Title = "Contract Registry request failed.",
            Extensions = { ["diagnostics"] = diagnostics }
        });
    }

    private bool Authorize(
        string policy,
        bool mutation,
        out string? actor)
    {
        actor = User.Identity?.IsAuthenticated == true
            ? User.Identity.Name
            : Request.Headers["X-Operator-Id"].FirstOrDefault();
        return authorizer.IsAuthorized(User, policy, actor, mutation);
    }

    private bool Can(string policy)
    {
        var actor = User.Identity?.IsAuthenticated == true
            ? User.Identity.Name
            : Request.Headers["X-Operator-Id"].FirstOrDefault();
        return authorizer.IsAuthorized(User, policy, actor, false);
    }

    private IActionResult AuthorizationFailure() =>
        User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

    private bool TryToken(out long token, out IActionResult? error)
    {
        token = 0;
        error = null;
        var raw = Request.Headers.IfMatch.FirstOrDefault()?.Trim('"');
        if (raw is null)
        {
            error = StatusCode(
                StatusCodes.Status428PreconditionRequired,
                new ProblemDetails { Title = "If-Match is required." });
            return false;
        }
        if (!long.TryParse(raw, out token))
        {
            ModelState.AddModelError(
                "If-Match",
                "If-Match must contain a numeric concurrency token.");
            error = ValidationProblem(ModelState);
            return false;
        }
        return true;
    }

    private string RequestId() =>
        Request.Headers["X-Request-Id"].FirstOrDefault()
        ?? HttpContext.TraceIdentifier;

    private static string Quote(long value) => $"\"{value}\"";
}
