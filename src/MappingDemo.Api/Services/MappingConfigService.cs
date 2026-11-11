using System.Text.Json;
using Dapper;
using MappingDemo.Api.Contracts.MappingConfigs;
using MappingDemo.Api.Options;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Serialization;
using MappingDemo.Shared.Transformation;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class MappingConfigService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string SelectMappingConfigByIdSql = """
        SELECT
            id AS "Id",
            name AS "Name",
            input_folder AS "InputFolder",
            source_table_id AS "SourceTableId",
            normalized_table_id AS "NormalizedTableId",
            active_version_id AS "ActiveVersionId",
            COALESCE(
                (SELECT destination_definition::text
                 FROM mapping_config_versions
                 WHERE id = mapping_configs.active_version_id),
                mapping_configs.draft_destination_definition::text)
                AS "DestinationDefinitionJson"
        FROM mapping_configs
        WHERE id = @Id;
        """;

    private const string SelectMappingConfigsWithVersionsSql = """
        SELECT
            mapping_configs.id AS "ConfigId",
            mapping_configs.name AS "Name",
            mapping_configs.input_folder AS "InputFolder",
            mapping_configs.source_table_id AS "SourceTableId",
            mapping_configs.normalized_table_id AS "NormalizedTableId",
            mapping_configs.active_version_id AS "ActiveVersionId",
            mapping_configs.draft_destination_definition::text
                AS "DraftDestinationDefinitionJson",
            mapping_config_versions.id AS "VersionId",
            mapping_config_versions.version_no AS "VersionNo",
            mapping_config_versions.input_layout::text
                AS "InputLayoutJson",
            mapping_config_versions.transformation_program::text
                AS "TransformationProgramJson",
            mapping_config_versions.destination_definition::text
                AS "DestinationDefinitionJson",
            mapping_config_versions.accepted_extensions::text
                AS "AcceptedExtensionsJson",
            mapping_config_versions.contract_schema_snapshot::text
                AS "ContractSchemaSnapshotJson",
            mapping_config_versions.contract_schema_hash
                AS "ContractSchemaHash",
            mapping_config_versions.contract_schema_fetched_at
                AS "ContractSchemaFetchedAt",
            mapping_config_versions.payload_bindings_snapshot::text
                AS "PayloadBindingsSnapshotJson"
        FROM mapping_configs
        LEFT JOIN mapping_config_versions
            ON mapping_config_versions.config_id = mapping_configs.id
        ORDER BY mapping_configs.id, mapping_config_versions.version_no;
        """;

    private const string SelectMappingConfigWithVersionsByIdSql = """
        SELECT
            mapping_configs.id AS "ConfigId",
            mapping_configs.name AS "Name",
            mapping_configs.input_folder AS "InputFolder",
            mapping_configs.source_table_id AS "SourceTableId",
            mapping_configs.normalized_table_id AS "NormalizedTableId",
            mapping_configs.active_version_id AS "ActiveVersionId",
            mapping_configs.draft_destination_definition::text
                AS "DraftDestinationDefinitionJson",
            mapping_config_versions.id AS "VersionId",
            mapping_config_versions.version_no AS "VersionNo",
            mapping_config_versions.input_layout::text
                AS "InputLayoutJson",
            mapping_config_versions.transformation_program::text
                AS "TransformationProgramJson",
            mapping_config_versions.destination_definition::text
                AS "DestinationDefinitionJson",
            mapping_config_versions.accepted_extensions::text
                AS "AcceptedExtensionsJson",
            mapping_config_versions.contract_schema_snapshot::text
                AS "ContractSchemaSnapshotJson",
            mapping_config_versions.contract_schema_hash
                AS "ContractSchemaHash",
            mapping_config_versions.contract_schema_fetched_at
                AS "ContractSchemaFetchedAt",
            mapping_config_versions.payload_bindings_snapshot::text
                AS "PayloadBindingsSnapshotJson"
        FROM mapping_configs
        LEFT JOIN mapping_config_versions
            ON mapping_config_versions.config_id = mapping_configs.id
        WHERE mapping_configs.id = @Id
        ORDER BY mapping_config_versions.version_no;
        """;

    private const string InsertMappingConfigSql = """
        INSERT INTO mapping_configs (
            name,
            input_folder,
            source_table_id,
            normalized_table_id,
            draft_destination_definition)
        VALUES (
            @Name,
            @InputFolder,
            @SourceTableId,
            @NormalizedTableId,
            CAST(@DraftDestinationDefinitionJson AS jsonb))
        RETURNING id;
        """;

    private const string SelectNextVersionNumberSql = """
        SELECT COALESCE(MAX(version_no), 0) + 1
        FROM mapping_config_versions
        WHERE config_id = @ConfigId;
        """;

    private const string SelectLegacyNormalizedTableIdSql = """
        SELECT normalized_table_id
        FROM mapping_configs
        WHERE id = @ConfigId;
        """;

    private const string InsertMappingConfigVersionSql = """
        INSERT INTO mapping_config_versions (
            config_id,
            version_no,
            input_layout,
            transformation_program,
            destination_definition,
            accepted_extensions,
            api_destination_profile_id,
            api_destination_profile_version_id,
            contract_reference,
            contract_schema_snapshot,
            contract_schema_hash,
            contract_schema_fetched_at,
            payload_bindings_snapshot)
        VALUES (
            @ConfigId,
            @VersionNo,
            CAST(@InputLayoutJson AS jsonb),
            CAST(@TransformationProgramJson AS jsonb),
            CAST(@DestinationDefinitionJson AS jsonb),
            CAST(@AcceptedExtensionsJson AS jsonb),
            @ApiDestinationProfileId,
            @ApiDestinationProfileVersionId,
            @ContractReference,
            CAST(@ContractSchemaSnapshotJson AS jsonb),
            @ContractSchemaHash,
            @ContractSchemaFetchedAt,
            CAST(@PayloadBindingsSnapshotJson AS jsonb))
        RETURNING id;
        """;

    private const string SelectMappingConfigVersionSql = """
        SELECT
            id AS "Id",
            config_id AS "ConfigId",
            version_no AS "VersionNo",
            input_layout::text AS "InputLayoutJson",
            transformation_program::text AS "TransformationProgramJson",
            destination_definition::text AS "DestinationDefinitionJson",
            accepted_extensions::text AS "AcceptedExtensionsJson",
            contract_schema_snapshot::text AS "ContractSchemaSnapshotJson",
            contract_schema_hash AS "ContractSchemaHash",
            contract_schema_fetched_at AS "ContractSchemaFetchedAt",
            payload_bindings_snapshot::text AS "PayloadBindingsSnapshotJson"
        FROM mapping_config_versions
        WHERE config_id = @ConfigId
          AND version_no = @VersionNo;
        """;

    private const string ActivateMappingConfigVersionSql = """
        UPDATE mapping_config_versions
        SET activated_at = now()
        WHERE id = @VersionId
          AND config_id = @ConfigId;
        """;

    private const string SetActiveMappingConfigVersionSql = """
        UPDATE mapping_configs
        SET active_version_id = @VersionId
        WHERE id = @ConfigId;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly string _inputRootPath;
    private readonly ContractRegistryModule? _contractRegistry;

    public MappingConfigService(
        NpgsqlDataSource dataSource,
        IOptions<PathOptions> pathOptions,
        IHostEnvironment environment,
        ContractRegistryModule? contractRegistry = null)
    {
        _dataSource = dataSource;
        _inputRootPath = Path.GetFullPath(
            pathOptions.Value.InputRoot,
            environment.ContentRootPath);
        _contractRegistry = contractRegistry;
    }

    public async Task<long> CreateAsync(
        CreateMappingConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var command = new CommandDefinition(
            InsertMappingConfigSql,
            new
            {
                request.Name,
                request.InputFolder,
                request.SourceTableId,
                request.NormalizedTableId,
                DraftDestinationDefinitionJson = request.Destination is null
                    ? null
                    : JsonSerializer.Serialize(request.Destination, JsonOptions)
            },
            transaction,
            cancellationToken: cancellationToken);
        var configId = await connection.ExecuteScalarAsync<long>(command);

        var inputFolderPath = Path.Combine(
            _inputRootPath,
            request.InputFolder);
        Directory.CreateDirectory(inputFolderPath);

        await transaction.CommitAsync(cancellationToken);

        return configId;
    }

    public async Task<MappingConfigResponse?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectMappingConfigByIdSql,
            new { Id = id },
            cancellationToken: cancellationToken);

        var row = await connection
            .QuerySingleOrDefaultAsync<MappingConfigRow>(command);
        return row is null
            ? null
            : new MappingConfigResponse(
                row.Id,
                row.Name,
                row.InputFolder,
                row.SourceTableId,
                row.NormalizedTableId,
                row.ActiveVersionId,
                DeserializeDestination(
                    row.DestinationDefinitionJson,
                    row.NormalizedTableId));
    }

    public async Task<IReadOnlyList<MappingConfigDetailsResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectMappingConfigsWithVersionsSql,
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<MappingConfigDetailsRow>(
            command);

        return MapConfigDetails(rows);
    }

    public async Task<MappingConfigDetailsResponse?> GetDetailsByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectMappingConfigWithVersionsByIdSql,
            new { Id = id },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<MappingConfigDetailsRow>(
            command);

        return MapConfigDetails(rows).SingleOrDefault();
    }

    public async Task<MappingConfigVersionResponse> CreateVersionAsync(
        long configId,
        CreateMappingConfigVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var destination = request.Destination;
        if (destination is null)
        {
            await using var lookupConnection =
                await _dataSource.OpenConnectionAsync(cancellationToken);
            var legacyTableId = await lookupConnection.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    SelectLegacyNormalizedTableIdSql,
                    new { ConfigId = configId },
                    cancellationToken: cancellationToken));
            destination = legacyTableId is > 0
                ? new LegacyTableDestination(legacyTableId.Value)
                : throw new InvalidOperationException(
                    "Destination is required when normalizedTableId is absent.");
        }

        ContractSnapshotResult? contractSnapshot = null;
        if (destination is ApiContractDestination or JsonFileDestination)
        {
            if (_contractRegistry is null)
            {
                throw new InvalidOperationException("Contract Registry Module is not configured.");
            }
            contractSnapshot = await _contractRegistry.ResolveAsync(
                destination,
                cancellationToken);
            if (!contractSnapshot.IsSuccess)
            {
                throw new ContractResolutionException(contractSnapshot.Diagnostics);
            }
            destination = destination switch
            {
                ApiContractDestination api => api with
                {
                    Contract = contractSnapshot.Contract!
                },
                JsonFileDestination file => file with
                {
                    Contract = contractSnapshot.Contract!
                },
                _ => destination
            };
        }

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var nextVersionCommand = new CommandDefinition(
            SelectNextVersionNumberSql,
            new { ConfigId = configId },
            transaction,
            cancellationToken: cancellationToken);
        var versionNo = await connection.ExecuteScalarAsync<int>(
            nextVersionCommand);
        var acceptedExtensions = NormalizeExtensions(
            request.AcceptedExtensions,
            request.InputLayout);
        var parameters = new
        {
            ConfigId = configId,
            VersionNo = versionNo,
            InputLayoutJson = JsonSerializer.Serialize(
                request.InputLayout,
                JsonOptions),
            TransformationProgramJson = JsonSerializer.Serialize(
                request.TransformationProgram,
                JsonOptions),
            DestinationDefinitionJson = JsonSerializer.Serialize(
                destination,
                JsonOptions),
            AcceptedExtensionsJson = JsonSerializer.Serialize(
                acceptedExtensions,
                JsonOptions),
            ApiDestinationProfileId =
                (destination as ApiContractDestination)?.ApiDestinationProfileId,
            ApiDestinationProfileVersionId = contractSnapshot?.Profile
                ?.ProfileVersionId is > 0
                    ? (long?)contractSnapshot.Profile.ProfileVersionId
                    : null,
            ContractReference = destination switch
            {
                ApiContractDestination api => api.Contract.Reference,
                JsonFileDestination file => file.Contract.Reference,
                _ => null
            },
            ContractSchemaSnapshotJson = contractSnapshot?.CanonicalSchema,
            ContractSchemaHash = contractSnapshot?.SchemaHash,
            ContractSchemaFetchedAt = contractSnapshot?.FetchedAt,
            PayloadBindingsSnapshotJson = contractSnapshot is null
                ? null
                : JsonSerializer.Serialize(
                    contractSnapshot.PayloadBindings,
                    JsonOptions)
        };
        var insertCommand = new CommandDefinition(
            InsertMappingConfigVersionSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);
        var versionId = await connection.ExecuteScalarAsync<long>(
            insertCommand);

        await transaction.CommitAsync(cancellationToken);

        return new MappingConfigVersionResponse(
            versionId,
            configId,
            versionNo,
            request.InputLayout,
            request.TransformationProgram,
            destination,
            acceptedExtensions,
            ToSnapshotResponse(contractSnapshot));
    }

    public async Task<MappingConfigVersionResponse?> GetVersionAsync(
        long configId,
        int versionNo,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectMappingConfigVersionSql,
            new
            {
                ConfigId = configId,
                VersionNo = versionNo
            },
            cancellationToken: cancellationToken);
        var row = await connection
            .QuerySingleOrDefaultAsync<MappingConfigVersionRow>(command);

        if (row is null)
        {
            return null;
        }

        var inputLayout = PolymorphicJson.Deserialize<InputLayoutDefinition>(
            row.InputLayoutJson,
            JsonOptions) ?? throw new JsonException(
                "Persisted input layout cannot be null.");
        var transformationProgram =
            PolymorphicJson.Deserialize<TransformationProgram>(
                row.TransformationProgramJson,
                JsonOptions) ?? throw new JsonException(
                "Persisted transformation program cannot be null.");
        var destination = DeserializeDestination(
            row.DestinationDefinitionJson,
            legacyNormalizedTableId: null) ?? throw new JsonException(
                "Persisted destination definition cannot be null.");
        var acceptedExtensions = JsonSerializer.Deserialize<string[]>(
            row.AcceptedExtensionsJson,
            JsonOptions) ?? [];

        return new MappingConfigVersionResponse(
            row.Id,
            row.ConfigId,
            row.VersionNo,
            inputLayout,
            transformationProgram,
            destination,
            acceptedExtensions,
            ToSnapshotResponse(row));
    }

    public async Task ActivateVersionAsync(
        long configId,
        long versionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var parameters = new
        {
            ConfigId = configId,
            VersionId = versionId
        };
        var activateVersionCommand = new CommandDefinition(
            ActivateMappingConfigVersionSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(activateVersionCommand);

        var setActiveVersionCommand = new CommandDefinition(
            SetActiveMappingConfigVersionSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(setActiveVersionCommand);

        await transaction.CommitAsync(cancellationToken);
    }

    private static IReadOnlyList<MappingConfigDetailsResponse>
        MapConfigDetails(IEnumerable<MappingConfigDetailsRow> rows)
    {
        return rows
            .GroupBy(row => row.ConfigId)
            .Select(group =>
            {
                var config = group.First();
                var versions = group
                    .Where(row => row.VersionId.HasValue)
                    .OrderBy(row => row.VersionNo)
                    .Select(row => new MappingConfigVersionDetailsResponse(
                        row.VersionId!.Value,
                        row.VersionNo!.Value,
                        row.VersionId == config.ActiveVersionId,
                        PolymorphicJson.Deserialize<InputLayoutDefinition>(
                            row.InputLayoutJson!,
                            JsonOptions) ?? throw new JsonException(
                                "Persisted input layout cannot be null."),
                        PolymorphicJson.Deserialize<TransformationProgram>(
                            row.TransformationProgramJson!,
                            JsonOptions) ?? throw new JsonException(
                            "Persisted transformation program cannot be null."),
                        DeserializeDestination(
                            row.DestinationDefinitionJson,
                            config.NormalizedTableId) ?? throw new JsonException(
                            "Persisted destination definition cannot be null."),
                        JsonSerializer.Deserialize<string[]>(
                            row.AcceptedExtensionsJson!,
                            JsonOptions) ?? [],
                        ToSnapshotResponse(row)))
                    .ToArray();

                return new MappingConfigDetailsResponse(
                    config.ConfigId,
                    config.Name,
                    config.InputFolder,
                    config.SourceTableId,
                    config.NormalizedTableId,
                    config.ActiveVersionId,
                    versions
                        .SingleOrDefault(version => version.IsActive)
                        ?.Destination
                        ?? DeserializeDestination(
                            config.DraftDestinationDefinitionJson,
                            config.NormalizedTableId),
                    versions);
            })
            .ToArray();
    }

    private sealed class MappingConfigDetailsRow
    {
        public long ConfigId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string InputFolder { get; init; } = string.Empty;

        public long SourceTableId { get; init; }

        public long? NormalizedTableId { get; init; }

        public long? ActiveVersionId { get; init; }

        public string? DraftDestinationDefinitionJson { get; init; }

        public long? VersionId { get; init; }

        public int? VersionNo { get; init; }

        public string? InputLayoutJson { get; init; }

        public string? TransformationProgramJson { get; init; }

        public string? DestinationDefinitionJson { get; init; }

        public string? AcceptedExtensionsJson { get; init; }

        public string? ContractSchemaSnapshotJson { get; init; }

        public string? ContractSchemaHash { get; init; }

        public DateTimeOffset? ContractSchemaFetchedAt { get; init; }

        public string? PayloadBindingsSnapshotJson { get; init; }
    }

    private sealed class MappingConfigVersionRow
    {
        public long Id { get; init; }

        public long ConfigId { get; init; }

        public int VersionNo { get; init; }

        public string InputLayoutJson { get; init; } = string.Empty;

        public string TransformationProgramJson { get; init; } = string.Empty;

        public string DestinationDefinitionJson { get; init; } = string.Empty;

        public string AcceptedExtensionsJson { get; init; } = string.Empty;

        public string? ContractSchemaSnapshotJson { get; init; }

        public string? ContractSchemaHash { get; init; }

        public DateTimeOffset? ContractSchemaFetchedAt { get; init; }

        public string? PayloadBindingsSnapshotJson { get; init; }
    }

    private sealed class MappingConfigRow
    {
        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string InputFolder { get; init; } = string.Empty;

        public long SourceTableId { get; init; }

        public long? NormalizedTableId { get; init; }

        public long? ActiveVersionId { get; init; }

        public string? DestinationDefinitionJson { get; init; }
    }

    private static DestinationDefinition? DeserializeDestination(
        string? json,
        long? legacyNormalizedTableId)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            return PolymorphicJson.Deserialize<DestinationDefinition>(
                json,
                JsonOptions);
        }

        return legacyNormalizedTableId is > 0
            ? new LegacyTableDestination(legacyNormalizedTableId.Value)
            : null;
    }

    private static IReadOnlyList<string> NormalizeExtensions(
        IReadOnlyList<string>? requested,
        InputLayoutDefinition layout)
    {
        return (requested ?? InputFileExtensionPolicy.DefaultsFor(layout))
            .Select(InputFileExtensionPolicy.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ContractSnapshotResponse? ToSnapshotResponse(
        ContractSnapshotResult? snapshot) =>
        snapshot is null
            ? null
            : new ContractSnapshotResponse(
                snapshot.CanonicalSchema!,
                snapshot.SchemaHash!,
                snapshot.FetchedAt!.Value,
                snapshot.PathCatalog,
                snapshot.PayloadBindings);

    private static ContractSnapshotResponse? ToSnapshotResponse(
        MappingConfigVersionRow row) =>
        ToSnapshotResponse(
            row.ContractSchemaSnapshotJson,
            row.ContractSchemaHash,
            row.ContractSchemaFetchedAt,
            row.PayloadBindingsSnapshotJson);

    private static ContractSnapshotResponse? ToSnapshotResponse(
        MappingConfigDetailsRow row) =>
        ToSnapshotResponse(
            row.ContractSchemaSnapshotJson,
            row.ContractSchemaHash,
            row.ContractSchemaFetchedAt,
            row.PayloadBindingsSnapshotJson);

    private static ContractSnapshotResponse? ToSnapshotResponse(
        string? schema,
        string? hash,
        DateTimeOffset? fetchedAt,
        string? bindingsJson)
    {
        if (schema is null || hash is null || fetchedAt is null)
        {
            return null;
        }
        var compilation = new ContractSchemaModule().Compile(schema);
        if (!compilation.IsSuccess)
        {
            throw new JsonException("Persisted contract schema snapshot is invalid.");
        }
        var bindings = bindingsJson is null
            ? []
            : JsonSerializer.Deserialize<PayloadBinding[]>(bindingsJson, JsonOptions) ?? [];
        return new ContractSnapshotResponse(
            schema,
            hash,
            fetchedAt.Value,
            compilation.PathCatalog,
            bindings);
    }
}

public sealed class ContractResolutionException(
    IReadOnlyList<ContractDiagnostic> diagnostics)
    : Exception("Contract resolution failed.")
{
    public IReadOnlyList<ContractDiagnostic> Diagnostics { get; } = diagnostics;
}
