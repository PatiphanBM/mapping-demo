using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Contracts.Tables;
using MappingDemo.Api.Services;
using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("mapping-configs")]
public sealed class MappingConfigsController : ControllerBase
{
    private readonly MappingConfigService _mappingConfigService;
    private readonly TableService _tableService;
    private readonly TransformationModule _transformationModule;
    private readonly MappingDraftService _mappingDraftService;

    public MappingConfigsController(
        MappingConfigService mappingConfigService,
        TableService tableService,
        TransformationModule transformationModule,
        MappingDraftService mappingDraftService)
    {
        _mappingConfigService = mappingConfigService;
        _tableService = tableService;
        _transformationModule = transformationModule;
        _mappingDraftService = mappingDraftService;
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
            request.InputLayout,
            request.TransformationProgram,
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

    [HttpPost("{id:long}/versions/validate")]
    public async Task<IActionResult> ValidateVersion(
        long id,
        CreateMappingConfigVersionRequest request,
        CancellationToken cancellationToken)
    {
        var schemaResult = await GetSchemaAsync(id, cancellationToken);
        if (!schemaResult.ConfigFound)
        {
            return NotFound();
        }

        if (schemaResult.Schema is null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "A table used by the mapping config was not found.");
        }

        var response = await _mappingDraftService.ValidateAsync(
            request.InputLayout,
            request.TransformationProgram,
            schemaResult.Schema,
            cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:long}/versions/preview")]
    [RequestSizeLimit(MappingDraftService.MaxRequestBodyBytes)]
    public async Task<IActionResult> PreviewVersion(
        long id,
        PreviewMappingConfigVersionRequest request,
        CancellationToken cancellationToken)
    {
        var schemaResult = await GetSchemaAsync(id, cancellationToken);
        if (!schemaResult.ConfigFound)
        {
            return NotFound();
        }

        if (schemaResult.Schema is null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "A table used by the mapping config was not found.");
        }

        var result = await _mappingDraftService.PreviewAsync(
            request,
            schemaResult.Schema,
            cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Response);
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            ModelState.AddModelError(
                diagnostic.Path,
                $"{diagnostic.Code}: {diagnostic.Message}");
        }

        return ValidationProblem(ModelState);
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
            configVersion.InputLayout,
            configVersion.TransformationProgram,
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

        var compilation = _transformationModule.Compile(
            configVersion.TransformationProgram,
            new TransformationSchema(
                ToDefinition(sourceTable),
                ToDefinition(normalizedTable)));
        if (compilation.CompiledProgram is null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "A validated transformation program could not be compiled.");
        }

        var dependencyDiagnostics =
            await _transformationModule.ValidateDependenciesAsync(
                compilation.CompiledProgram,
                cancellationToken);
        foreach (var diagnostic in dependencyDiagnostics)
        {
            ModelState.AddModelError(
                $"transformationProgram.{diagnostic.Path}",
                diagnostic.Message);
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

    private async Task<SchemaResult> GetSchemaAsync(
        long configId,
        CancellationToken cancellationToken)
    {
        var config = await _mappingConfigService.GetByIdAsync(
            configId,
            cancellationToken);
        if (config is null)
        {
            return new SchemaResult(false, null);
        }

        var sourceTable = await _tableService.GetByIdAsync(
            config.SourceTableId,
            cancellationToken);
        var normalizedTable = await _tableService.GetByIdAsync(
            config.NormalizedTableId,
            cancellationToken);
        var schema = sourceTable is null || normalizedTable is null
            ? null
            : new TransformationSchema(
                ToDefinition(sourceTable),
                ToDefinition(normalizedTable));
        return new SchemaResult(true, schema);
    }

    private static TableDefinition ToDefinition(TableResponse table)
    {
        var columns = table.Columns
            .Select((column, index) => new ColumnDefinition(
                column.Name,
                column.DataType,
                column.IsRequired,
                index + 1,
                column.Length,
                column.Precision,
                column.Scale))
            .ToArray();

        return new TableDefinition(table.Name, table.Kind, columns);
    }

    private sealed record SchemaResult(
        bool ConfigFound,
        TransformationSchema? Schema);
}
