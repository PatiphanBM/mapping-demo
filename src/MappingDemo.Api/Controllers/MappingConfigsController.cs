using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Contracts.Tables;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json;
using MappingDemo.Shared.Serialization;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("mapping-configs")]
public sealed class MappingConfigsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions =
        ApiJsonOptions.CreateSerializerOptions();
    private readonly MappingConfigService _mappingConfigService;
    private readonly TableService _tableService;
    private readonly TransformationModule _transformationModule;
    private readonly MappingDraftService _mappingDraftService;
    private readonly bool _apiContractDestinationCreationEnabled;
    private readonly ContractRegistryModule _contractRegistry;
    private readonly IApiDestinationProfileCatalog _profiles;
    private readonly FileDestinationProfileCatalog _fileProfiles;
    private readonly PayloadBindingModule _payloadBindings;
    private readonly JsonFilePreviewModule _jsonFilePreviewModule;
    private readonly DeliveryOperationsService _deliveryOperations;

    public MappingConfigsController(
        MappingConfigService mappingConfigService,
        TableService tableService,
        TransformationModule transformationModule,
        MappingDraftService mappingDraftService,
        ContractRegistryModule contractRegistry,
        IApiDestinationProfileCatalog profiles,
        FileDestinationProfileCatalog fileProfiles,
        PayloadBindingModule payloadBindings,
        JsonFilePreviewModule jsonFilePreviewModule,
        DeliveryOperationsService deliveryOperations,
        IConfiguration configuration)
    {
        _mappingConfigService = mappingConfigService;
        _tableService = tableService;
        _transformationModule = transformationModule;
        _mappingDraftService = mappingDraftService;
        _contractRegistry = contractRegistry;
        _profiles = profiles;
        _fileProfiles = fileProfiles;
        _payloadBindings = payloadBindings;
        _jsonFilePreviewModule = jsonFilePreviewModule;
        _deliveryOperations = deliveryOperations;
        _apiContractDestinationCreationEnabled = configuration.GetValue(
            "Features:ApiContractDestinationCreation",
            false);
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var configs = await _mappingConfigService.GetAllAsync(
            cancellationToken);

        return Ok(configs.Select(WithFileDestinationProfile).ToArray());
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var config = await _mappingConfigService.GetDetailsByIdAsync(
            id,
            cancellationToken);

        return config is null
            ? NotFound()
            : Ok(WithFileDestinationProfile(config));
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

        var destination = NormalizeDestination(
            request.Destination,
            request.NormalizedTableId);

        var sourceTable = await _tableService.GetByIdAsync(
            request.SourceTableId,
            cancellationToken);
        TableResponse? normalizedTable = null;
        if (destination is LegacyTableDestination legacyDestination)
        {
            normalizedTable = await _tableService.GetByIdAsync(
                legacyDestination.NormalizedTableId,
                cancellationToken);
        }
        else if (destination is ApiContractDestination
                 && !_apiContractDestinationCreationEnabled)
        {
            ModelState.AddModelError(
                "destination.kind",
                "ApiContract destination creation is disabled until Phase 23.");
        }
        else if (destination is ApiContractDestination apiDestination)
        {
            var profile = await _profiles.ResolveActiveAsync(
                apiDestination.ApiDestinationProfileId,
                cancellationToken);
            foreach (var diagnostic in profile.Diagnostics)
            {
                ModelState.AddModelError(
                    $"destination.{diagnostic.Path}",
                    diagnostic.Message);
            }
        }
        else if (destination is JsonFileDestination fileDestination)
        {
            var profile = _fileProfiles.ResolveForCreation(
                fileDestination.FileDestinationProfileId);
            foreach (var diagnostic in profile.Diagnostics)
            {
                ModelState.AddModelError(
                    $"destination.{diagnostic.Path}",
                    diagnostic.Message);
            }
        }

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

        if (destination is LegacyTableDestination && normalizedTable is null)
        {
            ModelState.AddModelError(
                "normalizedTableId",
                "Normalized table was not found.");
        }
        else if (normalizedTable is not null
                 && normalizedTable.Kind != TableKind.Normalized)
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
                request with
                {
                    NormalizedTableId =
                        (destination as LegacyTableDestination)
                        ?.NormalizedTableId,
                    Destination = destination
                },
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
            (destination as LegacyTableDestination)?.NormalizedTableId,
            ActiveVersionId: null,
            destination,
            GetFileDestinationProfile(destination));

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

        var destination = NormalizeDestination(
            request.Destination,
            config.NormalizedTableId);
        if (destination is null)
        {
            return ValidationProblem(ModelState);
        }
        if (destination is ApiContractDestination
            && !_apiContractDestinationCreationEnabled)
        {
            ModelState.AddModelError(
                "destination.kind",
                "ApiContract destination creation is disabled until Phase 23.");
            return ValidationProblem(ModelState);
        }

        var sourceTable = await _tableService.GetByIdAsync(
            config.SourceTableId,
            cancellationToken);
        if (sourceTable is null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "A table used by the mapping config was not found.");
        }

        var schemaResult = await GetSchemaAsync(config, destination, cancellationToken);
        AddContractDiagnostics(schemaResult.Diagnostics);
        var errors = schemaResult.Schema is null
            ? []
            : ConfigVersionValidator.Validate(
                request.InputLayout,
                request.TransformationProgram,
                schemaResult.Schema);

        ValidateAcceptedExtensions(
            request.AcceptedExtensions,
            request.InputLayout);

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
                request with { Destination = destination },
                cancellationToken);

            return Created(
                $"/mapping-configs/{id}/versions/{response.VersionNo}",
                response with
                {
                    FileDestinationProfile =
                        GetFileDestinationProfile(response.Destination)
                });
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
        catch (ContractResolutionException exception)
        {
            AddContractDiagnostics(exception.Diagnostics);
            return ValidationProblem(ModelState);
        }
    }

    [HttpPost("{id:long}/versions/validate")]
    public async Task<IActionResult> ValidateVersion(
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

        var destination = NormalizeDestination(
            request.Destination,
            config.NormalizedTableId);
        if (destination is null)
        {
            return ValidationProblem(ModelState);
        }
        if (destination is ApiContractDestination
            && !_apiContractDestinationCreationEnabled)
        {
            ModelState.AddModelError(
                "destination.kind",
                "ApiContract destination creation is disabled until Phase 23.");
            return ValidationProblem(ModelState);
        }

        var schemaResult = await GetSchemaAsync(
            config,
            destination,
            cancellationToken);
        if (!schemaResult.ConfigFound)
        {
            return NotFound();
        }

        AddContractDiagnostics(schemaResult.Diagnostics);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
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
        ValidateAcceptedExtensions(
            request.AcceptedExtensions,
            request.InputLayout);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        return Ok(response);
    }

    [HttpPost("{id:long}/versions/preview")]
    [RequestSizeLimit(MappingDraftService.MaxRequestBodyBytes)]
    public async Task<IActionResult> PreviewVersion(
        long id,
        PreviewMappingConfigVersionRequest request,
        CancellationToken cancellationToken)
    {
        var config = await _mappingConfigService.GetByIdAsync(
            id,
            cancellationToken);
        if (config is null)
        {
            return NotFound();
        }

        var destination = NormalizeDestination(
            request.Destination,
            config.NormalizedTableId);
        if (destination is null)
        {
            return ValidationProblem(ModelState);
        }
        if (destination is ApiContractDestination
            && !_apiContractDestinationCreationEnabled)
        {
            ModelState.AddModelError(
                "destination.kind",
                "ApiContract destination creation is disabled until Phase 23.");
            return ValidationProblem(ModelState);
        }

        ValidateAcceptedExtensions(
            request.AcceptedExtensions,
            request.InputLayout);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var schemaResult = await GetSchemaAsync(
            config,
            destination,
            cancellationToken);
        if (!schemaResult.ConfigFound)
        {
            return NotFound();
        }

        AddContractDiagnostics(schemaResult.Diagnostics);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
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
            var response = await BuildDestinationPreviewAsync(
                id,
                destination,
                schemaResult.Snapshot,
                result.Response!,
                GetPreviewSourceFileName(request.Sample),
                cancellationToken);
            return Ok(response);
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

        if (configVersion.Destination is ApiContractDestination apiDestination)
        {
            var profile = await _profiles.ResolveActiveAsync(
                apiDestination.ApiDestinationProfileId,
                cancellationToken);
            if (!profile.IsSuccess)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "The API destination profile is unavailable."
                });
            }

            var gate = await _deliveryOperations.CanActivateApiContractAsync(
                id,
                profile.Profile!.Environment,
                cancellationToken);
            if (!gate.Allowed)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "ApiContract activation is blocked by rollout policy.",
                    Detail = gate.Reason
                });
            }
        }
        else if (configVersion.Destination is JsonFileDestination jsonDestination)
        {
            var profile = _fileProfiles.ResolveForCreation(
                jsonDestination.FileDestinationProfileId);
            if (!profile.IsSuccess)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "The File destination profile is unavailable."
                });
            }

            var gate = await _deliveryOperations.CanActivateJsonFileAsync(
                id,
                cancellationToken);
            if (!gate.Allowed)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "JsonFile activation is blocked by rollout policy.",
                    Detail = gate.Reason
                });
            }
        }

        var sourceTable = await _tableService.GetByIdAsync(
            config.SourceTableId,
            cancellationToken);
        if (sourceTable is null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "A table used by the mapping config was not found.");
        }

        TargetSchema? targetSchema = null;
        if (configVersion.Destination is LegacyTableDestination legacy)
        {
            var normalizedTable = await _tableService.GetByIdAsync(
                legacy.NormalizedTableId,
                cancellationToken);
            if (normalizedTable is not null)
            {
                targetSchema = new LegacyTableTargetSchemaAdapter().Adapt(
                    ToDefinition(normalizedTable));
            }
        }
        else if (configVersion.ContractSnapshot is not null)
        {
            targetSchema = new ContractSchemaModule()
                .Compile(configVersion.ContractSnapshot.CanonicalSchema)
                .TargetSchema;
        }
        if (targetSchema is null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "The pinned target schema was not found.");
        }

        var errors = ConfigVersionValidator.Validate(
            configVersion.InputLayout,
            configVersion.TransformationProgram,
            new TransformationSchema(
                ToDefinition(sourceTable),
                targetSchema,
                configVersion.ContractSnapshot?.PayloadBindings
                    .Select(binding => binding.TargetPath)
                    .ToHashSet(StringComparer.Ordinal)));

        ValidateAcceptedExtensions(
            configVersion.AcceptedExtensions,
            configVersion.InputLayout);

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
                targetSchema,
                configVersion.ContractSnapshot?.PayloadBindings
                    .Select(binding => binding.TargetPath)
                    .ToHashSet(StringComparer.Ordinal)));
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

    [HttpPost("{id:long}/versions/preview-file")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MappingDraftService.MaxUploadBytes + 1024 * 1024)]
    public async Task<IActionResult> PreviewFile(
        long id,
        [FromForm] PreviewMappingConfigFileForm form,
        CancellationToken cancellationToken)
    {
        if (form.File.Length > MappingDraftService.MaxUploadBytes)
        {
            ModelState.AddModelError("file", "FileTooLarge");
            return ValidationProblem(ModelState);
        }

        InputLayoutDefinition inputLayout;
        TransformationProgram transformationProgram;
        DestinationDefinition? requestedDestination;
        try
        {
            inputLayout = PolymorphicJson.Deserialize<InputLayoutDefinition>(
                form.InputLayoutJson,
                JsonOptions) ?? throw new JsonException("inputLayoutJson is null.");
            transformationProgram = PolymorphicJson.Deserialize<TransformationProgram>(
                form.TransformationProgramJson,
                JsonOptions) ?? throw new JsonException("transformationProgramJson is null.");
            requestedDestination = string.IsNullOrWhiteSpace(form.DestinationJson)
                ? null
                : PolymorphicJson.Deserialize<DestinationDefinition>(
                    form.DestinationJson,
                    JsonOptions);
        }
        catch (JsonException exception)
        {
            ModelState.AddModelError("form", $"InvalidPreviewJson: {exception.Message}");
            return ValidationProblem(ModelState);
        }

        var config = await _mappingConfigService.GetByIdAsync(id, cancellationToken);
        if (config is null)
        {
            return NotFound();
        }

        var destination = NormalizeDestination(
            requestedDestination,
            config.NormalizedTableId);
        if (destination is null)
        {
            return ValidationProblem(ModelState);
        }
        var schemaResult = await GetSchemaAsync(config, destination, cancellationToken);
        AddContractDiagnostics(schemaResult.Diagnostics);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        if (schemaResult.Schema is null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "A table used by the mapping config was not found.");
        }

        await using var stream = form.File.OpenReadStream();
        var result = await _mappingDraftService.PreviewFileAsync(
            stream,
            form.File.FileName,
            form.EvaluationAt,
            inputLayout,
            transformationProgram,
            schemaResult.Schema,
            cancellationToken);
        if (result.IsSuccess)
        {
            var response = await BuildDestinationPreviewAsync(
                id,
                destination,
                schemaResult.Snapshot,
                result.Response!,
                form.File.FileName,
                cancellationToken);
            return Ok(response);
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            ModelState.AddModelError(
                diagnostic.Path,
                $"{diagnostic.Code}: {diagnostic.Message}");
        }
        return ValidationProblem(ModelState);
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

    private void ValidateAcceptedExtensions(
        IReadOnlyList<string>? extensions,
        InputLayoutDefinition layout)
    {
        var values = extensions ?? InputFileExtensionPolicy.DefaultsFor(layout);
        if (values.Count == 0)
        {
            ModelState.AddModelError(
                "acceptedExtensions",
                "At least one accepted extension is required.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < values.Count; index++)
        {
            var normalized = InputFileExtensionPolicy.Normalize(values[index]);
            var path = $"acceptedExtensions[{index}]";
            if (!InputFileExtensionPolicy.IsSupported(normalized))
            {
                ModelState.AddModelError(path, "UnsupportedFileExtension");
            }
            else if (!seen.Add(normalized))
            {
                ModelState.AddModelError(path, "DuplicateFileExtension");
            }
            else if (!InputFileExtensionPolicy.IsCompatible(normalized, layout))
            {
                ModelState.AddModelError(path, "FileExtensionLayoutMismatch");
            }
        }
    }

    private async Task<SchemaResult> GetSchemaAsync(
        MappingConfigResponse config,
        DestinationDefinition destination,
        CancellationToken cancellationToken)
    {
        var sourceTable = await _tableService.GetByIdAsync(
            config.SourceTableId,
            cancellationToken);
        if (sourceTable is null)
        {
            return new SchemaResult(true, null, []);
        }
        if (destination is LegacyTableDestination legacy)
        {
            var normalizedTable = await _tableService.GetByIdAsync(
                legacy.NormalizedTableId,
                cancellationToken);
            var legacySchema = normalizedTable is null
                ? null
                : TransformationSchema.FromLegacyTable(
                    ToDefinition(sourceTable),
                    ToDefinition(normalizedTable));
            return new SchemaResult(true, legacySchema, []);
        }

        var snapshot = await _contractRegistry.ResolveAsync(
            destination,
            cancellationToken);
        return snapshot.IsSuccess
            ? new SchemaResult(
                true,
                new TransformationSchema(
                    ToDefinition(sourceTable),
                    snapshot.TargetSchema!,
                    snapshot.PayloadBindings
                        .Select(binding => binding.TargetPath)
                        .ToHashSet(StringComparer.Ordinal)),
                [],
                snapshot)
            : new SchemaResult(true, null, snapshot.Diagnostics, null);
    }

    private async Task<TableResponse?> GetLegacyTableAsync(
        DestinationDefinition destination,
        CancellationToken cancellationToken)
    {
        return destination is LegacyTableDestination legacy
            ? await _tableService.GetByIdAsync(
                legacy.NormalizedTableId,
                cancellationToken)
            : null;
    }

    private DestinationDefinition? NormalizeDestination(
        DestinationDefinition? destination,
        long? legacyNormalizedTableId)
    {
        var result = DestinationContractNormalizer.Normalize(
            destination,
            legacyNormalizedTableId);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(
                result.ErrorPath!,
                result.ErrorMessage!);
        }

        return result.Destination;
    }

    private async Task<PreviewMappingConfigVersionResponse>
        BuildDestinationPreviewAsync(
            long configId,
            DestinationDefinition destination,
            ContractSnapshotResult? snapshot,
            PreviewMappingConfigVersionResponse response,
            string sourceFileName,
            CancellationToken cancellationToken)
    {
        if (snapshot is null)
        {
            return response;
        }

        if (destination is JsonFileDestination jsonFile)
        {
            var preview = await _jsonFilePreviewModule.BuildAsync(
                new JsonFilePreviewRequest(
                    jsonFile,
                    snapshot.TargetSchema!,
                    snapshot.PayloadBindings,
                    response,
                    configId,
                    sourceFileName),
                cancellationToken);
            return response with { JsonFile = preview };
        }

        return destination is ApiContractDestination
            ? response with
            {
                Contract = BuildDeliveryPreview(
                    configId,
                    response,
                    snapshot,
                    sourceFileName)
            }
            : response;
    }

    private static string GetPreviewSourceFileName(MappingPreviewSample sample)
    {
        return sample switch
        {
            TextFilePreviewSample text => text.FileName,
            SourceRowsPreviewSample rows => rows.Rows
                .Select(row => row.FileName)
                .FirstOrDefault(fileName =>
                    !string.IsNullOrWhiteSpace(fileName))
                ?? "preview.json",
            _ => "preview.json"
        };
    }

    private MappingConfigResponse WithFileDestinationProfile(
        MappingConfigResponse response) =>
        response with
        {
            FileDestinationProfile =
                GetFileDestinationProfile(response.Destination)
        };

    private MappingConfigDetailsResponse WithFileDestinationProfile(
        MappingConfigDetailsResponse response) =>
        response with
        {
            FileDestinationProfile =
                GetFileDestinationProfile(response.Destination),
            Versions = response.Versions
                .Select(version => version with
                {
                    FileDestinationProfile =
                        GetFileDestinationProfile(version.Destination)
                })
                .ToArray()
        };

    private FileDestinationProfileSummaryResponse?
        GetFileDestinationProfile(DestinationDefinition? destination)
    {
        if (destination is not JsonFileDestination jsonFile)
        {
            return null;
        }

        var profile = _fileProfiles.GetSafeProfiles().SingleOrDefault(value =>
            string.Equals(
                value.Id,
                jsonFile.FileDestinationProfileId,
                StringComparison.Ordinal));
        return profile is null
            ? null
            : new FileDestinationProfileSummaryResponse(
                profile.Id,
                profile.Enabled,
                profile.SupportedContracts,
                profile.PayloadBindings);
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
        TransformationSchema? Schema,
        IReadOnlyList<ContractDiagnostic> Diagnostics,
        ContractSnapshotResult? Snapshot = null);

    private ContractDeliveryPreviewResponse BuildDeliveryPreview(
        long configId,
        PreviewMappingConfigVersionResponse response,
        ContractSnapshotResult snapshot,
        string sourceFileName)
    {
        var outputs = new List<DeliveryPreviewOutputResponse>();
        for (var rowIndex = 0; rowIndex < response.Records.Count; rowIndex++)
        {
            var record = response.Records[rowIndex];
            foreach (var output in record.Transformation?.Outputs ?? [])
            {
                var binding = _payloadBindings.Bind(
                    snapshot.TargetSchema!,
                    output.Values,
                    snapshot.PayloadBindings,
                    new PayloadBindingContext(
                        snapshot.Contract!.Reference,
                        FileJobId: 0,
                        SourceRowId: rowIndex + 1,
                        SourceRowNumber: record.RowNumber ?? rowIndex + 1,
                        ConfigVersionReference: $"preview:{configId}",
                        output.OutputKey,
                        FileName: sourceFileName));
                outputs.Add(new DeliveryPreviewOutputResponse(
                    record.RowKey,
                    output.OutputKey,
                    binding.Payload?.Json,
                    binding.Payload?.Hash,
                    binding.Diagnostics));
            }
        }

        return new ContractDeliveryPreviewResponse(
            snapshot.Profile!.Id,
            snapshot.Contract!.Reference,
            snapshot.SchemaHash!,
            snapshot.Profile.Method,
            snapshot.Profile.DeliveryPathTemplate,
            snapshot.Profile.AllowedHeaderNames,
            $"preview-only:{configId}:row:output",
            SendsRequest: false,
            outputs);
    }

    private void AddContractDiagnostics(
        IReadOnlyList<ContractDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            ModelState.AddModelError(
                $"destination.{diagnostic.Path}",
                $"{diagnostic.Code}: {diagnostic.Message}");
        }
    }
}
