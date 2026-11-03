using MappingDemo.Api.Contracts.Tables;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Tables;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("tables")]
public sealed class TablesController(
    TableService tableService,
    ITableSchemaInspector schemaInspector,
    ITableAdministrationAuthorizer authorizer,
    TableVerificationRateLimiter rateLimiter,
    TableAdministrationObservability observability,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("permissions")]
    public IActionResult GetPermissions()
    {
        if (!Authorize("TableAdmin.Read", mutation: false, out _))
        {
            return AuthorizationFailure();
        }

        var mutationsEnabled = configuration.GetValue(
            "TableAdministration:MutationsEnabled",
            false);
        return Ok(new TableAdminCapabilities(
            Can("TableAdmin.Read"),
            mutationsEnabled && Can("TableAdmin.Create"),
            mutationsEnabled && Can("TableAdmin.AddColumn"),
            mutationsEnabled));
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? search,
        [FromQuery] TableKind? kind,
        [FromQuery] TableVerificationStatus? status,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (!Authorize("TableAdmin.Read", mutation: false, out _))
        {
            return AuthorizationFailure();
        }
        if (limit is < 1 or > 100)
        {
            ModelState.AddModelError("limit", "Limit must be between 1 and 100.");
        }
        if (offset < 0)
        {
            ModelState.AddModelError("offset", "Offset cannot be negative.");
        }
        if (search?.Length > 63)
        {
            ModelState.AddModelError(
                "search",
                "Search cannot exceed 63 characters.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var tables = await tableService.GetAllAsync(
            search,
            kind,
            status,
            limit,
            offset,
            cancellationToken);
        return Ok(tables);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        if (!Authorize("TableAdmin.Read", mutation: false, out _))
        {
            return AuthorizationFailure();
        }

        var table = await tableService.GetByIdAsync(id, cancellationToken);
        return table is null ? NotFound() : Ok(table);
    }

    [HttpGet("{id:long}/verification")]
    public async Task<IActionResult> GetLatestVerification(
        long id,
        CancellationToken cancellationToken)
    {
        if (!Authorize("TableAdmin.Read", mutation: false, out _))
        {
            return AuthorizationFailure();
        }
        if (await tableService.GetByIdAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        var verification = await tableService.GetLatestVerificationAsync(
            id,
            cancellationToken);
        return verification is null ? NoContent() : Ok(verification);
    }

    [HttpGet("{id:long}/references")]
    public async Task<IActionResult> GetReferences(
        long id,
        CancellationToken cancellationToken)
    {
        if (!Authorize("TableAdmin.Read", mutation: false, out _))
        {
            return AuthorizationFailure();
        }
        if (await tableService.GetByIdAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        return Ok(await tableService.GetReferencesAsync(
            id,
            cancellationToken));
    }

    [HttpPost("{id:long}/verify")]
    public async Task<IActionResult> Verify(
        long id,
        CancellationToken cancellationToken)
    {
        if (!Authorize("TableAdmin.Read", mutation: false, out var actor))
        {
            return AuthorizationFailure();
        }
        var table = await tableService.GetByIdAsync(id, cancellationToken);
        if (table is null)
        {
            return NotFound();
        }
        if (!rateLimiter.TryAcquire(id))
        {
            observability.RecordRateLimited();
            Response.Headers.RetryAfter = "60";
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Table verification rate limit exceeded.",
                    Detail = "Wait before verifying this table again."
                });
        }

        await tableService.MarkVerifyingAsync(id, cancellationToken);
        var result = await schemaInspector.InspectAsync(
            id,
            ToDefinition(table),
            cancellationToken);
        await tableService.SaveVerificationAsync(
            id,
            result,
            actor,
            RequestId(),
            cancellationToken);
        observability.RecordVerification(result);
        return Ok(result);
    }

    [HttpPost("{id:long}/columns")]
    public async Task<IActionResult> AddColumn(
        long id,
        ColumnRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("TableAdmin.AddColumn", mutation: true, out var actor))
        {
            return AuthorizationFailure();
        }
        if (!TryAudit(actor!, out var audit, out var auditError))
        {
            return auditError!;
        }

        var table = await tableService.GetByIdAsync(id, cancellationToken);
        if (table is null)
        {
            return NotFound();
        }

        ValidateColumn(table, request);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var added = await tableService.AddColumnAsync(
                id,
                request,
                audit!,
                cancellationToken);
            observability.RecordMutation("add_column", added);
            return added ? NoContent() : NotFound();
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            observability.RecordMutation("add_column", success: false);
            await tableService.RecordFailureAuditAsync(
                id,
                "AddColumn",
                audit!,
                "DuplicateColumn",
                cancellationToken);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Column name already exists.",
                Detail = "Choose a unique column name and retry."
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateTableRequest request,
        CancellationToken cancellationToken)
    {
        if (!Authorize("TableAdmin.Create", mutation: true, out var actor))
        {
            return AuthorizationFailure();
        }
        if (!TryAudit(actor!, out var audit, out var auditError))
        {
            return auditError!;
        }

        var definition = ToDefinition(request);
        foreach (var error in TableDefinitionValidator.Validate(definition))
        {
            ModelState.AddModelError(error.Field, error.Message);
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var tableId = await tableService.CreateAsync(
                definition,
                audit!,
                cancellationToken);
            observability.RecordMutation("create", success: true);
            var response = new TableResponse(
                tableId,
                request.Name,
                request.Kind,
                request.Columns,
                CreatedAt: null,
                TableVerificationStatus.Unknown,
                VerificationCheckedAt: null);
            return CreatedAtAction(nameof(GetById), new { id = tableId }, response);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            observability.RecordMutation("create", success: false);
            await tableService.RecordFailureAuditAsync(
                tableId: null,
                "Create",
                audit!,
                "DuplicateTable",
                cancellationToken);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Table name already exists.",
                Detail = "Choose a unique table name and retry."
            });
        }
    }

    private void ValidateColumn(TableResponse table, ColumnRequest request)
    {
        var column = new ColumnDefinition(
            request.Name,
            request.DataType,
            request.IsRequired,
            Ordinal: 1,
            request.Length,
            request.Precision,
            request.Scale);
        var definition = new TableDefinition(table.Name, table.Kind, [column]);
        foreach (var error in TableDefinitionValidator.Validate(definition))
        {
            const string prefix = "columns[0].";
            var field = error.Field.StartsWith(prefix, StringComparison.Ordinal)
                ? error.Field[prefix.Length..]
                : error.Field;
            ModelState.AddModelError(field, error.Message);
        }
        if (table.Columns.Any(existing =>
                string.Equals(
                    existing.Name,
                    request.Name,
                    StringComparison.Ordinal)))
        {
            ModelState.AddModelError("name", "Column name already exists.");
        }
    }

    private bool TryAudit(
        string actor,
        out TableAuditContext? audit,
        out IActionResult? error)
    {
        audit = null;
        error = null;
        var reason = Request.Headers["X-Change-Reason"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            ModelState.AddModelError(
                "X-Change-Reason",
                "X-Change-Reason is required and cannot exceed 500 characters.");
            error = ValidationProblem(ModelState);
            return false;
        }
        audit = new(actor, reason.Trim(), RequestId());
        return true;
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
        return authorizer.IsAuthorized(User, policy, actor, mutation: false);
    }

    private IActionResult AuthorizationFailure() =>
        User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

    private string RequestId() =>
        Request.Headers["X-Request-Id"].FirstOrDefault()
        ?? HttpContext.TraceIdentifier;

    private static TableDefinition ToDefinition(CreateTableRequest request) =>
        new(
            request.Name,
            request.Kind,
            request.Columns.Select((column, index) => new ColumnDefinition(
                column.Name,
                column.DataType,
                column.IsRequired,
                index + 1,
                column.Length,
                column.Precision,
                column.Scale)).ToArray());

    private static TableDefinition ToDefinition(TableResponse table) =>
        new(
            table.Name,
            table.Kind,
            table.Columns.Select((column, index) => new ColumnDefinition(
                column.Name,
                column.DataType,
                column.IsRequired,
                index + 1,
                column.Length,
                column.Precision,
                column.Scale)).ToArray());
}
