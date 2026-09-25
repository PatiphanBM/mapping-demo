using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Contracts.Tables;
using MappingDemo.Api.Services;
using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Tables;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("mapping-configs")]
public sealed class MappingConfigsController : ControllerBase
{
    private readonly MappingConfigService _mappingConfigService;
    private readonly TableService _tableService;

    public MappingConfigsController(
        MappingConfigService mappingConfigService,
        TableService tableService)
    {
        _mappingConfigService = mappingConfigService;
        _tableService = tableService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var configs = await _mappingConfigService.GetAllAsync(
            cancellationToken);

        return Ok(configs);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var config = await _mappingConfigService.GetDetailsByIdAsync(
            id,
            cancellationToken);

        return config is null ? NotFound() : Ok(config);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateMappingConfigRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError("name", "Name is required.");
        }

        if (!IsValidInputFolder(request.InputFolder))
        {
            ModelState.AddModelError(
                "inputFolder",
                "Input folder must be a single folder name.");
        }

        var sourceTable = await _tableService.GetByIdAsync(
            request.SourceTableId,
            cancellationToken);
        var normalizedTable = await _tableService.GetByIdAsync(
            request.NormalizedTableId,
            cancellationToken);

        if (sourceTable is null)
        {
            ModelState.AddModelError(
                "sourceTableId",
                "Source table was not found.");
        }
        else if (sourceTable.Kind != TableKind.Source)
        {
            ModelState.AddModelError(
                "sourceTableId",
                "The selected table must be a Source table.");
        }

        if (normalizedTable is null)
        {
            ModelState.AddModelError(
                "normalizedTableId",
                "Normalized table was not found.");
        }
        else if (normalizedTable.Kind != TableKind.Normalized)
        {
            ModelState.AddModelError(
                "normalizedTableId",
                "The selected table must be a Normalized table.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        long configId;

        try
        {
            configId = await _mappingConfigService.CreateAsync(
                request,
                cancellationToken);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Mapping config already exists.",
                Detail = "A mapping config with the same name or input folder " +
                         "already exists."
            });
        }

        var response = new MappingConfigResponse(
            configId,
            request.Name,
            request.InputFolder,
            request.SourceTableId,
            request.NormalizedTableId,
            ActiveVersionId: null);

        return Created($"/mapping-configs/{configId}", response);
    }

    [HttpPost("{id:long}/versions")]
    public async Task<IActionResult> CreateVersion(
        long id,
        CreateMappingConfigVersionRequest request,
        CancellationToken cancellationToken)
    {
        var config = await _mappingConfigService.GetByIdAsync(
            id,
            cancellationToken);

        if (config is null)
        {
            return NotFound();
        }

        var sourceTable = await _tableService.GetByIdAsync(
            config.SourceTableId,
            cancellationToken);
        var normalizedTable = await _tableService.GetByIdAsync(
            config.NormalizedTableId,
            cancellationToken);

        if (sourceTable is null || normalizedTable is null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "A table used by the mapping config was not found.");
        }

        var errors = ConfigVersionValidator.Validate(
            request.FileToSource,
            request.SourceToNormalized,
            ToDefinition(sourceTable),
            ToDefinition(normalizedTable));

        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var response = await _mappingConfigService.CreateVersionAsync(
                id,
                request,
                cancellationToken);

            return Created(
                $"/mapping-configs/{id}/versions/{response.VersionNo}",
                response);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Mapping config version conflict.",
                Detail = "Another version was created concurrently. " +
                         "Retry the request."
            });
        }
    }

    [HttpPost("{id:long}/versions/{version:int}/activate")]
    public async Task<IActionResult> ActivateVersion(
        long id,
        int version,
        CancellationToken cancellationToken)
    {
        var config = await _mappingConfigService.GetByIdAsync(
            id,
            cancellationToken);

        if (config is null)
        {
            return NotFound();
        }

        var configVersion = await _mappingConfigService.GetVersionAsync(
            id,
            version,
            cancellationToken);

        if (configVersion is null)
        {
            return NotFound();
        }

        var sourceTable = await _tableService.GetByIdAsync(
            config.SourceTableId,
            cancellationToken);
        var normalizedTable = await _tableService.GetByIdAsync(
            config.NormalizedTableId,
            cancellationToken);

        if (sourceTable is null || normalizedTable is null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "A table used by the mapping config was not found.");
        }

        var errors = ConfigVersionValidator.Validate(
            configVersion.FileToSource,
            configVersion.SourceToNormalized,
            ToDefinition(sourceTable),
            ToDefinition(normalizedTable));

        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await _mappingConfigService.ActivateVersionAsync(
            id,
            configVersion.Id,
            cancellationToken);

        return NoContent();
    }

    private static bool IsValidInputFolder(string? inputFolder)
    {
        return !string.IsNullOrWhiteSpace(inputFolder) &&
               inputFolder is not "." and not ".." &&
               !Path.IsPathRooted(inputFolder) &&
               !inputFolder.Contains('/') &&
               !inputFolder.Contains('\\') &&
               inputFolder.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static TableDefinition ToDefinition(TableResponse table)
    {
        var columns = table.Columns
            .Select((column, index) => new ColumnDefinition(
                column.Name,
                column.DataType,
                column.IsRequired,
                index + 1))
            .ToArray();

        return new TableDefinition(table.Name, table.Kind, columns);
    }
}
