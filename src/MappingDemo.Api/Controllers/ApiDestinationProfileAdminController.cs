using MappingDemo.Api.Contracts.ApiDestinationProfiles;
using MappingDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("admin/api-destination-profiles")]
public sealed class ApiDestinationProfileAdminController(
    ApiDestinationProfileAdministrationService service,
    IProfileAdministrationAuthorizer authorizer) : ControllerBase
{
    [HttpGet("permissions")]
    public IActionResult GetPermissions()
    {
        if (!Authorize("ProfileAdmin.Read", false, out _))
        {
            return AuthorizationFailure();
        }
        return Ok(new ApiDestinationProfileAdminCapabilitiesResponse(
            CanPolicy("ProfileAdmin.EndpointRead"),
            CanPolicy("ProfileAdmin.WriteDraft"),
            CanPolicy("ProfileAdmin.Activate"),
            CanPolicy("ProfileAdmin.ActivateProduction"),
            CanPolicy("ProfileAdmin.Disable")));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        if (!Authorize("ProfileAdmin.Read", false, out _))
        {
            return AuthorizationFailure();
        }
        var includeEndpoints = CanReadEndpoints();
        var profiles = await service.GetAllAsync(limit, offset, cancellationToken);
        return Ok(profiles.Select(profile =>
            Project(profile, includeEndpoints)));
    }

    [HttpGet("{profileKey}")]
    public async Task<IActionResult> Get(
        string profileKey,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ProfileAdmin.Read", false, out _))
        {
            return AuthorizationFailure();
        }
        var profile = await service.GetAsync(profileKey, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }
        Response.Headers.ETag = Quote(profile.ConcurrencyToken);
        return Ok(Project(profile, CanReadEndpoints()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateApiDestinationProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ProfileAdmin.WriteDraft", true, out var actor))
        {
            return AuthorizationFailure();
        }
        try
        {
            var created = await service.CreateAsync(
                request, actor!, RequestId(), cancellationToken);
            return CreatedAtAction(nameof(Get),
                new { profileKey = created.ProfileKey }, created);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            return ConflictProblem("Profile or Draft already exists.");
        }
        catch (ArgumentException exception)
        {
            return ValidationProblemFor(
                exception.ParamName ?? "request", exception.Message);
        }
    }

    [HttpPost("{profileKey}/versions")]
    public async Task<IActionResult> CreateDraft(
        string profileKey,
        [FromQuery] int? sourceVersionNo,
        ApiDestinationProfileActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ProfileAdmin.WriteDraft", true, out var actor))
        {
            return AuthorizationFailure();
        }
        if (!TryToken(out var token, out var error))
        {
            return error!;
        }
        try
        {
            var result = await service.CreateDraftAsync(profileKey,
                sourceVersionNo, token, actor!, request.Reason, RequestId(),
                cancellationToken);
            if (result is not null)
            {
                return Ok(result);
            }
            var current = await service.GetAsync(profileKey, cancellationToken);
            return current is null
                ? NotFound()
                : PreconditionFailed(
                    "Profile changed or is unavailable.",
                    current.ConcurrencyToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            return ConflictProblem("A Draft already exists.");
        }
        catch (ArgumentException exception)
        {
            return ValidationProblemFor(
                exception.ParamName ?? "request", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ConflictProblem(exception.Message);
        }
    }

    [HttpPut("{profileKey}/versions/{versionNo:int}")]
    public async Task<IActionResult> UpdateDraft(
        string profileKey,
        int versionNo,
        UpdateApiDestinationProfileDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ProfileAdmin.WriteDraft", true, out var actor))
        {
            return AuthorizationFailure();
        }
        if (!TryToken(out var token, out var error))
        {
            return error!;
        }
        try
        {
            var result = await service.UpdateDraftAsync(profileKey, versionNo,
                token, request, actor!, RequestId(), cancellationToken);
            if (result is not null)
            {
                return Ok(result);
            }
            var current = await service.GetAsync(profileKey, cancellationToken);
            var currentToken = current?.Versions.SingleOrDefault(version =>
                version.VersionNo == versionNo)?.ConcurrencyToken;
            return PreconditionFailed(
                "Draft changed or is unavailable.", currentToken);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblemFor(
                exception.ParamName ?? "request", exception.Message);
        }
        catch (PostgresException exception) when (
            exception.SqlState is "23505" or "23514")
        {
            return ValidationProblemFor(
                "draft", "The draft contains duplicate or invalid values.");
        }
    }

    [HttpPost("{profileKey}/versions/{versionNo:int}/validate")]
    public async Task<IActionResult> Validate(
        string profileKey,
        int versionNo,
        ApiDestinationProfileActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ProfileAdmin.WriteDraft", true, out var actor))
        {
            return AuthorizationFailure();
        }
        try
        {
            var result = await service.ValidateAsync(profileKey, versionNo,
                actor!, request.Reason, RequestId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblemFor(
                exception.ParamName ?? "request", exception.Message);
        }
    }

    [HttpPost("{profileKey}/versions/{versionNo:int}/activate")]
    public Task<IActionResult> Activate(
        string profileKey,
        int versionNo,
        ApiDestinationProfileActionRequest request,
        CancellationToken cancellationToken) =>
        ChangeActive(profileKey, versionNo, request, false, cancellationToken);

    [HttpPost("{profileKey}/versions/{versionNo:int}/rollback")]
    public Task<IActionResult> Rollback(
        string profileKey,
        int versionNo,
        ApiDestinationProfileActionRequest request,
        CancellationToken cancellationToken) =>
        ChangeActive(profileKey, versionNo, request, true, cancellationToken);

    [HttpPost("{profileKey}/disable")]
    public async Task<IActionResult> Disable(
        string profileKey,
        ApiDestinationProfileActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ProfileAdmin.Disable", true, out var actor))
        {
            return AuthorizationFailure();
        }
        if (!TryToken(out var token, out var error))
        {
            return error!;
        }
        try
        {
            var result = await service.DisableAsync(profileKey, token, actor!,
                request.Reason, RequestId(), cancellationToken);
            if (result is not null)
            {
                return Ok(result);
            }
            var current = await service.GetAsync(profileKey, cancellationToken);
            return PreconditionFailed(
                "Profile changed or is unavailable.",
                current?.ConcurrencyToken);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblemFor(
                exception.ParamName ?? "request", exception.Message);
        }
    }

    private async Task<IActionResult> ChangeActive(
        string profileKey,
        int versionNo,
        ApiDestinationProfileActionRequest request,
        bool rollback,
        CancellationToken cancellationToken)
    {
        if (!Authorize("ProfileAdmin.Activate", true, out var actor))
        {
            return AuthorizationFailure();
        }
        if (!TryToken(out var token, out var error))
        {
            return error!;
        }
        var detail = await service.GetAsync(profileKey, cancellationToken);
        var target = detail?.Versions.SingleOrDefault(version =>
            version.VersionNo == versionNo);
        if (detail is null || target is null)
        {
            return NotFound();
        }
        if (target.Environment == "Production" &&
            !Authorize("ProfileAdmin.ActivateProduction", true, out actor))
        {
            return AuthorizationFailure();
        }
        try
        {
            var result = rollback
                ? await service.RollbackAsync(profileKey, versionNo, token,
                    actor!, request.Reason, RequestId(), cancellationToken)
                : await service.ActivateAsync(profileKey, versionNo, token,
                    actor!, request.Reason, RequestId(), cancellationToken);
            return result is null
                ? ConflictProblem("Profile version is stale, invalid, or unavailable.")
                : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblemFor(
                exception.ParamName ?? "request", exception.Message);
        }
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

    private bool CanReadEndpoints()
    {
        var actor = User.Identity?.IsAuthenticated == true
            ? User.Identity.Name
            : Request.Headers["X-Operator-Id"].FirstOrDefault();
        return authorizer.IsAuthorized(
            User, "ProfileAdmin.EndpointRead", actor, false);
    }

    private bool CanPolicy(string policy)
    {
        var actor = User.Identity?.IsAuthenticated == true
            ? User.Identity.Name
            : Request.Headers["X-Operator-Id"].FirstOrDefault();
        return authorizer.IsAuthorized(User, policy, actor, false);
    }

    private static ApiDestinationProfileAdminResponse Project(
        ApiDestinationProfileAdminResponse profile,
        bool includeEndpoints) =>
        includeEndpoints
            ? profile
            : profile with
            {
                Versions = profile.Versions.Select(version => version with
                {
                    SchemaBaseUrl = null,
                    DeliveryBaseUrl = null,
                    AuthSecretReference = null
                }).ToArray()
            };

    private IActionResult AuthorizationFailure() =>
        User.Identity?.IsAuthenticated == true
            ? Forbid()
            : Unauthorized();

    private bool TryToken(out long token, out IActionResult? error)
    {
        token = 0;
        error = null;
        var raw = Request.Headers.IfMatch.FirstOrDefault()?.Trim('"');
        if (raw is null)
        {
            error = StatusCode(StatusCodes.Status428PreconditionRequired,
                new ProblemDetails { Title = "If-Match is required." });
            return false;
        }
        if (!long.TryParse(raw, out token))
        {
            error = ValidationProblemFor("If-Match",
                "If-Match must contain a numeric concurrency token.");
            return false;
        }
        return true;
    }

    private string RequestId() =>
        Request.Headers["X-Request-Id"].FirstOrDefault()
        ?? HttpContext.TraceIdentifier;

    private ObjectResult ConflictProblem(string title) =>
        Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = title
        });

    private IActionResult ValidationProblemFor(string path, string message)
    {
        ModelState.AddModelError(path, message);
        return ValidationProblem(ModelState);
    }

    private static ObjectResult PreconditionFailed(
        string title,
        long? currentToken)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status412PreconditionFailed,
            Title = title
        };
        if (currentToken is not null)
        {
            problem.Extensions["currentConcurrencyToken"] = currentToken.Value;
        }
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status412PreconditionFailed
        };
    }

    private static string Quote(long value) => $"\"{value}\"";
}
