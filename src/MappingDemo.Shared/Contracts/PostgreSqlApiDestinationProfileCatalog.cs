using Dapper;
using Npgsql;

namespace MappingDemo.Shared.Contracts;

public sealed class PostgreSqlApiDestinationProfileCatalog(
    NpgsqlDataSource dataSource) : IApiDestinationProfileCatalog
{
    private const string ActiveIdsSql = """
        SELECT active_version_id
        FROM api_destination_profiles
        WHERE active_version_id IS NOT NULL
        ORDER BY display_name, profile_key;
        """;

    private const string ActiveVersionIdSql = """
        SELECT active_version_id AS "ActiveVersionId"
        FROM api_destination_profiles
        WHERE profile_key = @ProfileKey;
        """;

    private const string ActiveVersionSql = """
        SELECT
            profiles.profile_key AS "Id",
            profiles.display_name AS "DisplayName",
            versions.id AS "ProfileVersionId",
            versions.version_no AS "VersionNo",
            versions.lifecycle AS "Lifecycle",
            versions.environment AS "Environment",
            versions.schema_base_url AS "SchemaBaseUrl",
            versions.schema_path_template AS "SchemaPathTemplate",
            versions.delivery_base_url AS "DeliveryBaseUrl",
            versions.delivery_path_template AS "DeliveryPathTemplate",
            versions.method AS "Method",
            versions.auth_scheme AS "AuthScheme",
            versions.auth_secret_reference AS "AuthSecretReference",
            versions.network_policy_reference AS "NetworkPolicyReference",
            versions.timeout_seconds AS "TimeoutSeconds",
            versions.idempotency_header_name AS "IdempotencyHeaderName",
            versions.correlation_header_name AS "CorrelationHeaderName",
            versions.allowed_header_names AS "AllowedHeaderNames",
            versions.content_hash AS "ContentHash"
        FROM api_destination_profiles AS profiles
        INNER JOIN api_destination_profile_versions AS versions
            ON versions.id = profiles.active_version_id
        WHERE profiles.profile_key = @ProfileKey;
        """;

    private const string ActiveContractsSql = """
        SELECT
            contracts.contract_id AS "Id",
            contracts.contract_version AS "Version",
            contracts.contract_reference AS "Reference"
        FROM api_destination_profiles AS profiles
        INNER JOIN api_destination_profile_contracts AS contracts
            ON contracts.profile_version_id = profiles.active_version_id
        WHERE profiles.profile_key = @ProfileKey
        ORDER BY contracts.ordinal;
        """;

    private const string ActiveBindingsSql = """
        SELECT
            bindings.target_path AS "TargetPath",
            bindings.runtime_value_source AS "SourceText"
        FROM api_destination_profiles AS profiles
        INNER JOIN api_destination_profile_bindings AS bindings
            ON bindings.profile_version_id = profiles.active_version_id
        WHERE profiles.profile_key = @ProfileKey
        ORDER BY bindings.ordinal;
        """;

    private const string VersionSql = """
        SELECT
            profiles.profile_key AS "Id",
            profiles.display_name AS "DisplayName",
            versions.id AS "ProfileVersionId",
            versions.version_no AS "VersionNo",
            versions.lifecycle AS "Lifecycle",
            versions.environment AS "Environment",
            versions.schema_base_url AS "SchemaBaseUrl",
            versions.schema_path_template AS "SchemaPathTemplate",
            versions.delivery_base_url AS "DeliveryBaseUrl",
            versions.delivery_path_template AS "DeliveryPathTemplate",
            versions.method AS "Method",
            versions.auth_scheme AS "AuthScheme",
            versions.auth_secret_reference AS "AuthSecretReference",
            versions.network_policy_reference AS "NetworkPolicyReference",
            versions.timeout_seconds AS "TimeoutSeconds",
            versions.idempotency_header_name AS "IdempotencyHeaderName",
            versions.correlation_header_name AS "CorrelationHeaderName",
            versions.allowed_header_names AS "AllowedHeaderNames",
            versions.content_hash AS "ContentHash"
        FROM api_destination_profile_versions AS versions
        INNER JOIN api_destination_profiles AS profiles
            ON profiles.id = versions.profile_id
        WHERE versions.id = @ProfileVersionId;
        """;

    private const string ActiveVersionsSql = """
        SELECT
            profiles.profile_key AS "Id",
            profiles.display_name AS "DisplayName",
            versions.id AS "ProfileVersionId",
            versions.version_no AS "VersionNo",
            versions.lifecycle AS "Lifecycle",
            versions.environment AS "Environment",
            versions.schema_base_url AS "SchemaBaseUrl",
            versions.schema_path_template AS "SchemaPathTemplate",
            versions.delivery_base_url AS "DeliveryBaseUrl",
            versions.delivery_path_template AS "DeliveryPathTemplate",
            versions.method AS "Method",
            versions.auth_scheme AS "AuthScheme",
            versions.auth_secret_reference AS "AuthSecretReference",
            versions.network_policy_reference AS "NetworkPolicyReference",
            versions.timeout_seconds AS "TimeoutSeconds",
            versions.idempotency_header_name AS "IdempotencyHeaderName",
            versions.correlation_header_name AS "CorrelationHeaderName",
            versions.allowed_header_names AS "AllowedHeaderNames",
            versions.content_hash AS "ContentHash"
        FROM api_destination_profile_versions AS versions
        INNER JOIN api_destination_profiles AS profiles
            ON profiles.id = versions.profile_id
        WHERE versions.id = ANY(@ProfileVersionIds)
        ORDER BY profiles.display_name, profiles.profile_key;
        """;

    private const string ContractsSql = """
        SELECT
            contract_id AS "Id",
            contract_version AS "Version",
            contract_reference AS "Reference"
        FROM api_destination_profile_contracts
        WHERE profile_version_id = @ProfileVersionId
        ORDER BY ordinal;
        """;

    private const string BindingsSql = """
        SELECT
            target_path AS "TargetPath",
            runtime_value_source AS "SourceText"
        FROM api_destination_profile_bindings
        WHERE profile_version_id = @ProfileVersionId
        ORDER BY ordinal;
        """;

    private const string ContractsByVersionsSql = """
        SELECT
            profile_version_id AS "ProfileVersionId",
            contract_id AS "Id",
            contract_version AS "Version",
            contract_reference AS "Reference"
        FROM api_destination_profile_contracts
        WHERE profile_version_id = ANY(@ProfileVersionIds)
        ORDER BY profile_version_id, ordinal;
        """;

    private const string BindingsByVersionsSql = """
        SELECT
            profile_version_id AS "ProfileVersionId",
            target_path AS "TargetPath",
            runtime_value_source AS "SourceText"
        FROM api_destination_profile_bindings
        WHERE profile_version_id = ANY(@ProfileVersionIds)
        ORDER BY profile_version_id, ordinal;
        """;

    public async ValueTask<IReadOnlyList<ApiDestinationProfileMetadata>>
        GetSafeProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var ids = (await connection.QueryAsync<long>(new CommandDefinition(
            ActiveIdsSql,
            cancellationToken: cancellationToken))).ToArray();
        if (ids.Length == 0)
        {
            return [];
        }
        var rows = (await connection.QueryAsync<ProfileRow>(
            new CommandDefinition(
                ActiveVersionsSql,
                new { ProfileVersionIds = ids },
                cancellationToken: cancellationToken))).ToArray();
        var contracts = (await connection.QueryAsync<VersionContractRow>(
            new CommandDefinition(
                ContractsByVersionsSql,
                new { ProfileVersionIds = ids },
                cancellationToken: cancellationToken))).ToLookup(
                    item => item.ProfileVersionId);
        var bindings = (await connection.QueryAsync<VersionBindingRow>(
            new CommandDefinition(
                BindingsByVersionsSql,
                new { ProfileVersionIds = ids },
                cancellationToken: cancellationToken))).ToLookup(
                    item => item.ProfileVersionId);

        return rows.Select(row =>
            CreateProfile(
                row,
                contracts[row.ProfileVersionId].Select(item =>
                    new SupportedContract(
                        item.Id, item.Version, item.Reference)).ToArray(),
                bindings[row.ProfileVersionId].Select(item =>
                    new PayloadBinding(
                        item.TargetPath,
                        Enum.Parse<RuntimeValueSource>(item.SourceText)))
                    .ToArray()))
            .Select(profile =>
            {
                _ = new ApiDestinationProfileCatalog([profile]);
                return ToMetadata(profile);
            })
            .ToArray();
    }

    public async ValueTask<ProfileResolutionResult> ResolveActiveAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(
                $"{ActiveVersionIdSql}\n{ActiveVersionSql}\n" +
                $"{ActiveContractsSql}\n{ActiveBindingsSql}",
                new { ProfileKey = profileId },
                cancellationToken: cancellationToken));
        var pointer = await results.ReadSingleOrDefaultAsync<ActivePointerRow>();
        var row = await results.ReadSingleOrDefaultAsync<ProfileRow>();
        var contracts = (await results.ReadAsync<SupportedContract>()).ToArray();
        var bindings = (await results.ReadAsync<BindingRow>())
            .Select(value => new PayloadBinding(
                value.TargetPath,
                Enum.Parse<RuntimeValueSource>(value.SourceText)))
            .ToArray();
        if (pointer is null)
        {
            return ProfileResolutionResult.Failure(
                "UnknownProfile",
                $"API destination profile '{profileId}' does not exist.");
        }

        if (pointer.ActiveVersionId is null)
        {
            return ProfileResolutionResult.Failure(
                "ProfileDisabled",
                $"API destination profile '{profileId}' is unavailable.");
        }
        if (row is null)
        {
            return ProfileResolutionResult.Failure(
                "InvalidActiveProfileVersion",
                "The active API destination profile state is inconsistent.");
        }

        var profile = CreateProfile(row, contracts, bindings);
        try
        {
            _ = new ApiDestinationProfileCatalog([profile]);
        }
        catch (ApiDestinationProfileValidationException exception)
        {
            return new ProfileResolutionResult(null, exception.Diagnostics);
        }
        return ProfileResolutionResult.Success(profile);
    }

    public async ValueTask<ProfileResolutionResult> ResolvePinnedAsync(
        long profileVersionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        return await ResolvePinnedAsync(
            connection,
            profileVersionId,
            cancellationToken);
    }

    private static async ValueTask<ProfileResolutionResult> ResolvePinnedAsync(
        NpgsqlConnection connection,
        long profileVersionId,
        CancellationToken cancellationToken)
    {
        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(
                $"{VersionSql}\n{ContractsSql}\n{BindingsSql}",
                new { ProfileVersionId = profileVersionId },
                cancellationToken: cancellationToken));
        var row = await results.ReadSingleOrDefaultAsync<ProfileRow>();
        var contracts = (await results.ReadAsync<SupportedContract>()).ToArray();
        var bindings = (await results.ReadAsync<BindingRow>())
            .Select(value => new PayloadBinding(
                value.TargetPath,
                Enum.Parse<RuntimeValueSource>(value.SourceText)))
            .ToArray();
        if (row is null)
        {
            return ProfileResolutionResult.Failure(
                "UnknownPinnedProfileVersion",
                $"API destination profile version '{profileVersionId}' does not exist.");
        }

        if (row.Lifecycle is not ("Active" or "Disabled"))
        {
            return ProfileResolutionResult.Failure(
                "PinnedProfileVersionNotImmutable",
                $"API destination profile version '{profileVersionId}' is not available for pinned resolution.");
        }
        var profile = CreateProfile(row, contracts, bindings);
        try
        {
            _ = new ApiDestinationProfileCatalog([profile]);
        }
        catch (ApiDestinationProfileValidationException exception)
        {
            return new ProfileResolutionResult(null, exception.Diagnostics);
        }

        return ProfileResolutionResult.Success(profile);
    }

    private static ApiDestinationProfile CreateProfile(
        ProfileRow row,
        IReadOnlyList<SupportedContract> contracts,
        IReadOnlyList<PayloadBinding> bindings) => new(
            row.Id,
            row.DisplayName,
            row.Lifecycle == "Active",
            row.SchemaBaseUrl,
            row.SchemaPathTemplate,
            row.DeliveryBaseUrl,
            row.DeliveryPathTemplate,
            row.Method,
            row.AllowedHeaderNames,
            row.AuthSecretReference ?? string.Empty,
            row.TimeoutSeconds,
            contracts,
            bindings,
            row.AuthScheme,
            row.IdempotencyHeaderName,
            row.CorrelationHeaderName,
            row.Environment,
            row.ProfileVersionId,
            row.VersionNo,
            row.Lifecycle,
            row.NetworkPolicyReference,
            row.ContentHash);

    private static ApiDestinationProfileMetadata ToMetadata(
        ApiDestinationProfile profile) => new(
        profile.Id,
        profile.DisplayName,
        profile.Enabled,
        profile.Method,
        profile.DeliveryPathTemplate,
        profile.Environment,
        profile.AllowedHeaderNames,
        profile.SupportedContracts,
        profile.PayloadBindings,
        profile.ProfileVersionId,
        profile.VersionNo,
        profile.Lifecycle,
        profile.ContentHash);

    private sealed class ProfileRow
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public long ProfileVersionId { get; init; }
        public int VersionNo { get; init; }
        public string Lifecycle { get; init; } = string.Empty;
        public string Environment { get; init; } = string.Empty;
        public string SchemaBaseUrl { get; init; } = string.Empty;
        public string SchemaPathTemplate { get; init; } = string.Empty;
        public string DeliveryBaseUrl { get; init; } = string.Empty;
        public string DeliveryPathTemplate { get; init; } = string.Empty;
        public string Method { get; init; } = string.Empty;
        public string AuthScheme { get; init; } = string.Empty;
        public string? AuthSecretReference { get; init; }
        public string NetworkPolicyReference { get; init; } = string.Empty;
        public int TimeoutSeconds { get; init; }
        public string IdempotencyHeaderName { get; init; } = string.Empty;
        public string CorrelationHeaderName { get; init; } = string.Empty;
        public string[] AllowedHeaderNames { get; init; } = [];
        public string ContentHash { get; init; } = string.Empty;
    }

    private sealed class ActivePointerRow
    {
        public long? ActiveVersionId { get; init; }
    }

    private sealed class BindingRow
    {
        public string TargetPath { get; init; } = string.Empty;
        public string SourceText { get; init; } = string.Empty;
    }

    private sealed record VersionContractRow(
        long ProfileVersionId,
        string Id,
        string Version,
        string Reference);

    private sealed record VersionBindingRow(
        long ProfileVersionId,
        string TargetPath,
        string SourceText);
}
