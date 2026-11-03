using System.Security.Cryptography;
using Dapper;
using MappingDemo.Api.Contracts.ApiDestinationProfiles;
using MappingDemo.Shared.Contracts;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class ApiDestinationProfileAdministrationService(
    NpgsqlDataSource dataSource,
    IProfileSecretReferenceValidator secretValidator,
    IApiDestinationProfileCatalogCacheInvalidator cacheInvalidator,
    ApiDestinationProfileObservability? observability = null)
{
    public async Task<IReadOnlyList<ApiDestinationProfileAdminResponse>> GetAllAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(offset, 0);
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var profiles = (await connection.QueryAsync<ProfileRow>(
            new CommandDefinition("""
                SELECT p.id AS "Id", p.profile_key AS "ProfileKey",
                       p.display_name AS "DisplayName",
                       p.active_version_id AS "ActiveVersionId",
                       p.concurrency_token AS "ConcurrencyToken",
                       active.version_no AS "ActiveVersionNo"
                FROM api_destination_profiles p
                LEFT JOIN api_destination_profile_versions active
                  ON active.id = p.active_version_id
                ORDER BY p.display_name, p.profile_key
                LIMIT @Limit OFFSET @Offset;
                """, new { Limit = limit, Offset = offset },
                cancellationToken: cancellationToken))).ToArray();
        if (profiles.Length == 0)
        {
            return [];
        }

        var profileIds = profiles.Select(profile => profile.Id).ToArray();
        var versions = (await connection.QueryAsync<VersionListRow>(
            new CommandDefinition("""
                SELECT profile_id AS "ProfileId",
                       id AS "ProfileVersionId", version_no AS "VersionNo",
                       lifecycle AS "Lifecycle", environment AS "Environment",
                       schema_base_url AS "SchemaBaseUrl",
                       schema_path_template AS "SchemaPathTemplate",
                       delivery_base_url AS "DeliveryBaseUrl",
                       delivery_path_template AS "DeliveryPathTemplate",
                       method AS "Method", auth_scheme AS "AuthScheme",
                       auth_secret_reference AS "AuthSecretReference",
                       network_policy_reference AS "NetworkPolicyReference",
                       timeout_seconds AS "TimeoutSeconds",
                       idempotency_header_name AS "IdempotencyHeaderName",
                       correlation_header_name AS "CorrelationHeaderName",
                       allowed_header_names AS "AllowedHeaderNames",
                       content_hash AS "ContentHash",
                       concurrency_token AS "ConcurrencyToken",
                       validated_at AS "ValidatedAt"
                FROM api_destination_profile_versions
                WHERE profile_id = ANY(@ProfileIds)
                ORDER BY profile_id, version_no DESC;
                """, new { ProfileIds = profileIds },
                cancellationToken: cancellationToken))).ToArray();
        var versionIds = versions
            .Select(version => version.ProfileVersionId)
            .ToArray();
        var contracts = versionIds.Length == 0
            ? []
            : (await connection.QueryAsync<VersionContractRow>(
                new CommandDefinition("""
                    SELECT profile_version_id AS "ProfileVersionId",
                           contract_id AS "Id", contract_version AS "Version",
                           contract_reference AS "Reference"
                    FROM api_destination_profile_contracts
                    WHERE profile_version_id = ANY(@VersionIds)
                    ORDER BY profile_version_id, ordinal;
                    """, new { VersionIds = versionIds },
                    cancellationToken: cancellationToken))).ToArray();
        var bindings = versionIds.Length == 0
            ? []
            : (await connection.QueryAsync<VersionBindingRow>(
                new CommandDefinition("""
                    SELECT profile_version_id AS "ProfileVersionId",
                           target_path AS "TargetPath",
                           runtime_value_source AS "SourceText"
                    FROM api_destination_profile_bindings
                    WHERE profile_version_id = ANY(@VersionIds)
                    ORDER BY profile_version_id, ordinal;
                    """, new { VersionIds = versionIds },
                    cancellationToken: cancellationToken))).ToArray();

        var contractsByVersion = contracts.ToLookup(
            item => item.ProfileVersionId);
        var bindingsByVersion = bindings.ToLookup(
            item => item.ProfileVersionId);
        var versionsByProfile = versions
            .Select(version => ToResponse(
                version,
                contractsByVersion[version.ProfileVersionId]
                    .Select(item => new SupportedContract(
                        item.Id, item.Version, item.Reference))
                    .ToArray(),
                bindingsByVersion[version.ProfileVersionId]
                    .Select(item => new PayloadBinding(
                        item.TargetPath,
                        Enum.Parse<RuntimeValueSource>(item.SourceText)))
                    .ToArray()))
            .ToLookup(item => item.ProfileId);

        return profiles.Select(profile =>
            new ApiDestinationProfileAdminResponse(
                profile.ProfileKey,
                profile.DisplayName,
                profile.ConcurrencyToken,
                profile.ActiveVersionNo,
                versionsByProfile[profile.Id]
                    .Select(item => item.Response)
                    .ToArray()))
            .ToArray();
    }

    public async Task<ApiDestinationProfileAdminResponse?> GetAsync(
        string profileKey,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var profile = await connection.QuerySingleOrDefaultAsync<ProfileRow>(
            new CommandDefinition("""
                SELECT id AS "Id", profile_key AS "ProfileKey",
                       display_name AS "DisplayName",
                       active_version_id AS "ActiveVersionId",
                       concurrency_token AS "ConcurrencyToken"
                FROM api_destination_profiles
                WHERE profile_key = @ProfileKey;
                """, new { ProfileKey = profileKey },
                cancellationToken: cancellationToken));
        if (profile is null)
        {
            return null;
        }
        var versionIds = (await connection.QueryAsync<long>(new CommandDefinition(
            """
            SELECT id FROM api_destination_profile_versions
            WHERE profile_id = @ProfileId ORDER BY version_no DESC;
            """, new { ProfileId = profile.Id },
            cancellationToken: cancellationToken))).ToArray();
        var versions = new List<ApiDestinationProfileVersionAdminResponse>();
        foreach (var versionId in versionIds)
        {
            versions.Add(await LoadVersionAsync(
                connection, versionId, cancellationToken));
        }
        return new ApiDestinationProfileAdminResponse(
            profile.ProfileKey,
            profile.DisplayName,
            profile.ConcurrencyToken,
            versions.SingleOrDefault(value =>
                value.ProfileVersionId == profile.ActiveVersionId)?.VersionNo,
            versions);
    }

    public async Task<ApiDestinationProfileAdminResponse> CreateAsync(
        CreateApiDestinationProfileRequest request,
        string actor,
        string requestId,
        CancellationToken cancellationToken)
    {
        ValidateReason(request.Reason);
        ValidateProfileIdentity(request.ProfileKey, request.DisplayName);
        var hash = ContentHash(request.Draft);
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var profileId = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition("""
                INSERT INTO api_destination_profiles (
                    profile_key, display_name, created_by, updated_by)
                VALUES (@ProfileKey, @DisplayName, @Actor, @Actor)
                RETURNING id;
                """, new
            {
                request.ProfileKey,
                request.DisplayName,
                Actor = actor
            }, transaction, cancellationToken: cancellationToken));
        var versionId = await InsertDraftAsync(
            connection, transaction, profileId, 1, request.Draft, hash,
            actor, cancellationToken);
        await InsertAuditAsync(connection, transaction, profileId, versionId,
            "CreateProfile", actor, request.Reason, requestId, null, "Draft",
            null, hash, 1, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetAsync(request.ProfileKey, cancellationToken))!;
    }

    public async Task<ApiDestinationProfileAdminResponse?> CreateDraftAsync(
        string profileKey,
        int? sourceVersionNo,
        long expectedToken,
        string actor,
        string reason,
        string requestId,
        CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var profile = await LockProfileAsync(
            connection, transaction, profileKey, cancellationToken);
        if (profile is null || profile.ConcurrencyToken != expectedToken)
        {
            return null;
        }
        var sourceId = sourceVersionNo is null
            ? profile.ActiveVersionId
            : await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                """
                SELECT id FROM api_destination_profile_versions
                WHERE profile_id = @ProfileId AND version_no = @VersionNo;
                """, new { ProfileId = profile.Id, VersionNo = sourceVersionNo },
                transaction, cancellationToken: cancellationToken));
        if (sourceId is null)
        {
            throw new InvalidOperationException("A source Profile Version is required.");
        }
        var source = await LoadVersionAsync(connection, sourceId.Value,
            cancellationToken, transaction);
        var next = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COALESCE(MAX(version_no), 0) + 1
            FROM api_destination_profile_versions WHERE profile_id = @ProfileId;
            """, new { ProfileId = profile.Id }, transaction,
            cancellationToken: cancellationToken));
        var draft = ToDraft(source);
        var versionId = await InsertDraftAsync(connection, transaction,
            profile.Id, next, draft, ContentHash(draft), actor,
            cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE api_destination_profiles
            SET concurrency_token = concurrency_token + 1,
                updated_at = now(), updated_by = @Actor
            WHERE id = @ProfileId;
            """, new { ProfileId = profile.Id, Actor = actor }, transaction,
            cancellationToken: cancellationToken));
        await InsertAuditAsync(connection, transaction, profile.Id, versionId,
            "CreateDraft", actor, reason, requestId, source.Lifecycle, "Draft",
            source.ContentHash, ContentHash(draft), expectedToken + 1,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(profileKey, cancellationToken);
    }

    public async Task<ApiDestinationProfileAdminResponse?> UpdateDraftAsync(
        string profileKey,
        int versionNo,
        long expectedToken,
        UpdateApiDestinationProfileDraftRequest request,
        string actor,
        string requestId,
        CancellationToken cancellationToken)
    {
        ValidateReason(request.Reason);
        var hash = ContentHash(request.Draft);
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var ids = await connection.QuerySingleOrDefaultAsync<VersionIdentity>(
            new CommandDefinition("""
                SELECT profiles.id AS "ProfileId", versions.id AS "VersionId",
                       versions.content_hash AS "ContentHash"
                FROM api_destination_profiles AS profiles
                INNER JOIN api_destination_profile_versions AS versions
                    ON versions.profile_id = profiles.id
                WHERE profiles.profile_key = @ProfileKey
                  AND versions.version_no = @VersionNo
                  AND versions.lifecycle = 'Draft'
                  AND versions.concurrency_token = @ExpectedToken
                FOR UPDATE OF versions;
                """, new { ProfileKey = profileKey, VersionNo = versionNo, ExpectedToken = expectedToken },
                transaction, cancellationToken: cancellationToken));
        if (ids is null)
        {
            return null;
        }
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE api_destination_profile_versions SET
                environment = @Environment,
                schema_base_url = @SchemaBaseUrl,
                schema_path_template = @SchemaPathTemplate,
                delivery_base_url = @DeliveryBaseUrl,
                delivery_path_template = @DeliveryPathTemplate,
                method = @Method,
                auth_scheme = @AuthScheme,
                auth_secret_reference = @AuthSecretReference,
                network_policy_reference = @NetworkPolicyReference,
                timeout_seconds = @TimeoutSeconds,
                idempotency_header_name = @IdempotencyHeaderName,
                correlation_header_name = @CorrelationHeaderName,
                allowed_header_names = @AllowedHeaderNames,
                content_hash = @ContentHash,
                concurrency_token = concurrency_token + 1,
                validated_at = NULL, validated_content_hash = NULL,
                updated_at = now(), updated_by = @Actor
            WHERE id = @VersionId;
            DELETE FROM api_destination_profile_contracts
            WHERE profile_version_id = @VersionId;
            DELETE FROM api_destination_profile_bindings
            WHERE profile_version_id = @VersionId;
            """, VersionParameters(ids.VersionId, request.Draft, hash, actor),
            transaction, cancellationToken: cancellationToken));
        await InsertChildrenAsync(connection, transaction, ids.VersionId,
            request.Draft, cancellationToken);
        await InsertAuditAsync(connection, transaction, ids.ProfileId,
            ids.VersionId, "UpdateDraft", actor, request.Reason, requestId,
            "Draft", "Draft", ids.ContentHash, hash, expectedToken + 1,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(profileKey, cancellationToken);
    }

    public async Task<ApiDestinationProfileValidationResponse?> ValidateAsync(
        string profileKey,
        int versionNo,
        string actor,
        string reason,
        string requestId,
        CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        var detail = await GetAsync(profileKey, cancellationToken);
        var version = detail?.Versions.SingleOrDefault(value =>
            value.VersionNo == versionNo && value.Lifecycle == "Draft");
        if (detail is null || version is null)
        {
            return null;
        }
        var domain = ToDomain(detail, version);
        var diagnostics = new List<ContractDiagnostic>();
        try
        {
            _ = new ApiDestinationProfileCatalog([domain]);
        }
        catch (ApiDestinationProfileValidationException exception)
        {
            diagnostics.AddRange(exception.Diagnostics);
        }
        var secret = await secretValidator.ValidateAsync(
            version.AuthScheme,
            version.AuthSecretReference,
            cancellationToken);
        if (!secret.IsValid)
        {
            diagnostics.Add(new ContractDiagnostic(
                "profiles.authSecretReference",
                secret.DiagnosticCode!,
                secret.DiagnosticMessage!));
        }
        var now = DateTimeOffset.UtcNow;
        if (diagnostics.Count == 0)
        {
            await using var connection =
                await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE api_destination_profile_versions
                SET validated_at = @ValidatedAt,
                    validated_content_hash = content_hash,
                    concurrency_token = concurrency_token + 1,
                    updated_at = now(), updated_by = @Actor
                WHERE id = @VersionId AND lifecycle = 'Draft';
                """, new
            {
                ValidatedAt = now,
                Actor = actor,
                VersionId = version.ProfileVersionId
            },
                transaction, cancellationToken: cancellationToken));
            await InsertAuditAsync(connection, transaction,
                await ProfileIdAsync(connection, transaction, profileKey, cancellationToken),
                version.ProfileVersionId, "ValidateDraft", actor, reason,
                requestId, "Draft", "Draft", version.ContentHash,
                version.ContentHash, version.ConcurrencyToken + 1,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        observability?.RecordValidation(
            version.Environment,
            diagnostics.Count == 0);
        return new ApiDestinationProfileValidationResponse(
            diagnostics.Count == 0,
            version.ContentHash,
            now,
            secret.Status,
            diagnostics);
    }

    public Task<ApiDestinationProfileAdminResponse?> ActivateAsync(
        string profileKey, int versionNo, long expectedToken, string actor,
        string reason, string requestId, CancellationToken cancellationToken) =>
        ChangeActiveAsync(profileKey, versionNo, expectedToken, actor, reason,
            requestId, false, cancellationToken);

    public Task<ApiDestinationProfileAdminResponse?> RollbackAsync(
        string profileKey, int versionNo, long expectedToken, string actor,
        string reason, string requestId, CancellationToken cancellationToken) =>
        ChangeActiveAsync(profileKey, versionNo, expectedToken, actor, reason,
            requestId, true, cancellationToken);

    public async Task<ApiDestinationProfileAdminResponse?> DisableAsync(
        string profileKey,
        long expectedToken,
        string actor,
        string reason,
        string requestId,
        CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var profile = await LockProfileAsync(connection, transaction,
            profileKey, cancellationToken);
        if (profile is null || profile.ConcurrencyToken != expectedToken)
        {
            return null;
        }
        if (profile.ActiveVersionId is { } active)
        {
            var before = await connection.QuerySingleAsync<string>(
                new CommandDefinition("SELECT content_hash FROM api_destination_profile_versions WHERE id = @Id;",
                    new { Id = active }, transaction,
                    cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE api_destination_profile_versions
                SET lifecycle = 'Disabled', disabled_at = now(), disabled_by = @Actor
                WHERE id = @Id;
                UPDATE api_destination_profiles
                SET active_version_id = NULL,
                    concurrency_token = concurrency_token + 1,
                    updated_at = now(), updated_by = @Actor
                WHERE id = @ProfileId;
                """, new { Id = active, ProfileId = profile.Id, Actor = actor },
                transaction, cancellationToken: cancellationToken));
            await InsertAuditAsync(connection, transaction, profile.Id, active,
                "Disable", actor, reason, requestId, "Active", "Disabled",
                before, before, expectedToken + 1, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        cacheInvalidator.InvalidateSafeProfiles();
        if (profile.ActiveVersionId is not null)
        {
            observability?.RecordMutation(
                ApiDestinationProfileMutationKind.Disable);
        }
        return await GetAsync(profileKey, cancellationToken);
    }

    private async Task<ApiDestinationProfileAdminResponse?> ChangeActiveAsync(
        string profileKey, int versionNo, long expectedToken, string actor,
        string reason, string requestId, bool rollback,
        CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var profile = await LockProfileAsync(connection, transaction,
            profileKey, cancellationToken);
        if (profile is null || profile.ConcurrencyToken != expectedToken)
        {
            return null;
        }
        var target = await connection.QuerySingleOrDefaultAsync<LifecycleRow>(
            new CommandDefinition("""
                SELECT id AS "Id", lifecycle AS "Lifecycle",
                       content_hash AS "ContentHash",
                       validated_content_hash AS "ValidatedContentHash"
                FROM api_destination_profile_versions
                WHERE profile_id = @ProfileId AND version_no = @VersionNo
                FOR UPDATE;
                """, new { ProfileId = profile.Id, VersionNo = versionNo },
                transaction, cancellationToken: cancellationToken));
        if (target is null || (!rollback &&
            (target.Lifecycle != "Draft" ||
             target.ValidatedContentHash != target.ContentHash)) ||
            (rollback && target.Lifecycle != "Disabled"))
        {
            return null;
        }
        if (profile.ActiveVersionId is { } activeId)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE api_destination_profile_versions
                SET lifecycle = 'Disabled', disabled_at = now(), disabled_by = @Actor
                WHERE id = @Id;
                """, new { Id = activeId, Actor = actor }, transaction,
                cancellationToken: cancellationToken));
        }
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE api_destination_profile_versions
            SET lifecycle = 'Active', activated_at = now(), activated_by = @Actor,
                disabled_at = NULL, disabled_by = NULL
            WHERE id = @VersionId;
            UPDATE api_destination_profiles
            SET active_version_id = @VersionId,
                concurrency_token = concurrency_token + 1,
                updated_at = now(), updated_by = @Actor
            WHERE id = @ProfileId;
            """, new { VersionId = target.Id, ProfileId = profile.Id, Actor = actor },
            transaction, cancellationToken: cancellationToken));
        await InsertAuditAsync(connection, transaction, profile.Id, target.Id,
            rollback ? "Rollback" : "Activate", actor, reason, requestId,
            target.Lifecycle, "Active", target.ContentHash,
            target.ContentHash, expectedToken + 1, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        cacheInvalidator.InvalidateSafeProfiles();
        observability?.RecordMutation(
            rollback
                ? ApiDestinationProfileMutationKind.Rollback
                : ApiDestinationProfileMutationKind.Activate);
        return await GetAsync(profileKey, cancellationToken);
    }

    private static async Task<long> InsertDraftAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        long profileId, int versionNo, ApiDestinationProfileDraftRequest draft,
        string hash, string actor, CancellationToken cancellationToken)
    {
        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO api_destination_profile_versions (
                profile_id, version_no, lifecycle, environment,
                schema_base_url, schema_path_template, delivery_base_url,
                delivery_path_template, method, auth_scheme,
                auth_secret_reference, network_policy_reference,
                timeout_seconds, idempotency_header_name,
                correlation_header_name, allowed_header_names,
                content_hash, created_by, updated_by)
            VALUES (
                @ProfileId, @VersionNo, 'Draft', @Environment,
                @SchemaBaseUrl, @SchemaPathTemplate, @DeliveryBaseUrl,
                @DeliveryPathTemplate, @Method, @AuthScheme,
                @AuthSecretReference, @NetworkPolicyReference,
                @TimeoutSeconds, @IdempotencyHeaderName,
                @CorrelationHeaderName, @AllowedHeaderNames,
                @ContentHash, @Actor, @Actor)
            RETURNING id;
            """, new
        {
            ProfileId = profileId,
            VersionNo = versionNo,
            draft.Environment,
            draft.SchemaBaseUrl,
            draft.SchemaPathTemplate,
            draft.DeliveryBaseUrl,
            draft.DeliveryPathTemplate,
            draft.Method,
            draft.AuthScheme,
            draft.AuthSecretReference,
            draft.NetworkPolicyReference,
            draft.TimeoutSeconds,
            draft.IdempotencyHeaderName,
            draft.CorrelationHeaderName,
            AllowedHeaderNames = draft.AllowedHeaderNames.ToArray(),
            ContentHash = hash,
            Actor = actor
        }, transaction, cancellationToken: cancellationToken));
        await InsertChildrenAsync(connection, transaction, id, draft,
            cancellationToken);
        return id;
    }

    private static async Task InsertChildrenAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        long versionId, ApiDestinationProfileDraftRequest draft,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < draft.SupportedContracts.Count; i++)
        {
            var item = draft.SupportedContracts[i];
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO api_destination_profile_contracts (
                    profile_version_id, ordinal, contract_id,
                    contract_version, contract_reference)
                VALUES (@VersionId, @Ordinal, @Id, @Version, @Reference);
                """, new { VersionId = versionId, Ordinal = i + 1, item.Id, item.Version, item.Reference },
                transaction, cancellationToken: cancellationToken));
        }
        for (var i = 0; i < draft.PayloadBindings.Count; i++)
        {
            var item = draft.PayloadBindings[i];
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO api_destination_profile_bindings (
                    profile_version_id, ordinal, target_path, runtime_value_source)
                VALUES (@VersionId, @Ordinal, @TargetPath, @Source);
                """, new { VersionId = versionId, Ordinal = i + 1, item.TargetPath, Source = item.Source.ToString() },
                transaction, cancellationToken: cancellationToken));
        }
    }

    private static async Task<ApiDestinationProfileVersionAdminResponse>
        LoadVersionAsync(NpgsqlConnection connection, long versionId,
            CancellationToken cancellationToken, NpgsqlTransaction? transaction = null)
    {
        var row = await connection.QuerySingleAsync<VersionRow>(new CommandDefinition("""
            SELECT id AS "ProfileVersionId", version_no AS "VersionNo",
                   lifecycle AS "Lifecycle", environment AS "Environment",
                   schema_base_url AS "SchemaBaseUrl",
                   schema_path_template AS "SchemaPathTemplate",
                   delivery_base_url AS "DeliveryBaseUrl",
                   delivery_path_template AS "DeliveryPathTemplate",
                   method AS "Method", auth_scheme AS "AuthScheme",
                   auth_secret_reference AS "AuthSecretReference",
                   network_policy_reference AS "NetworkPolicyReference",
                   timeout_seconds AS "TimeoutSeconds",
                   idempotency_header_name AS "IdempotencyHeaderName",
                   correlation_header_name AS "CorrelationHeaderName",
                   allowed_header_names AS "AllowedHeaderNames",
                   content_hash AS "ContentHash",
                   concurrency_token AS "ConcurrencyToken",
                   validated_at AS "ValidatedAt"
            FROM api_destination_profile_versions WHERE id = @VersionId;
            """, new { VersionId = versionId }, transaction,
            cancellationToken: cancellationToken));
        var contracts = (await connection.QueryAsync<SupportedContract>(new CommandDefinition("""
            SELECT contract_id AS "Id", contract_version AS "Version",
                   contract_reference AS "Reference"
            FROM api_destination_profile_contracts
            WHERE profile_version_id = @VersionId ORDER BY ordinal;
            """, new { VersionId = versionId }, transaction,
            cancellationToken: cancellationToken))).ToArray();
        var bindings = (await connection.QueryAsync<BindingRow>(new CommandDefinition("""
            SELECT target_path AS "TargetPath", runtime_value_source AS "SourceText"
            FROM api_destination_profile_bindings
            WHERE profile_version_id = @VersionId ORDER BY ordinal;
            """, new { VersionId = versionId }, transaction,
            cancellationToken: cancellationToken))).Select(value =>
                new PayloadBinding(value.TargetPath,
                    Enum.Parse<RuntimeValueSource>(value.SourceText))).ToArray();
        return new ApiDestinationProfileVersionAdminResponse(
            row.ProfileVersionId, row.VersionNo, row.Lifecycle, row.Environment,
            row.SchemaBaseUrl, row.SchemaPathTemplate, row.DeliveryBaseUrl,
            row.DeliveryPathTemplate, row.Method, row.AuthScheme,
            row.AuthSecretReference, row.NetworkPolicyReference,
            row.TimeoutSeconds, row.IdempotencyHeaderName,
            row.CorrelationHeaderName, row.AllowedHeaderNames, contracts,
            bindings, row.ContentHash, row.ConcurrencyToken, row.ValidatedAt);
    }

    private static async Task<ProfileRow?> LockProfileAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string profileKey, CancellationToken cancellationToken) =>
        await connection.QuerySingleOrDefaultAsync<ProfileRow>(new CommandDefinition("""
            SELECT id AS "Id", profile_key AS "ProfileKey",
                   display_name AS "DisplayName",
                   active_version_id AS "ActiveVersionId",
                   concurrency_token AS "ConcurrencyToken"
            FROM api_destination_profiles WHERE profile_key = @ProfileKey
            FOR UPDATE;
            """, new { ProfileKey = profileKey }, transaction,
            cancellationToken: cancellationToken));

    private static Task<long> ProfileIdAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        string profileKey, CancellationToken cancellationToken) =>
        connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT id FROM api_destination_profiles WHERE profile_key = @ProfileKey;",
            new { ProfileKey = profileKey }, transaction,
            cancellationToken: cancellationToken));

    private static async Task InsertAuditAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        long profileId, long? versionId, string action, string actor,
        string reason, string requestId, string? beforeLifecycle,
        string? afterLifecycle, string? beforeHash, string? afterHash,
        long token, CancellationToken cancellationToken) =>
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO api_destination_profile_audit (
                profile_id, profile_version_id, action, actor, reason,
                request_id, before_lifecycle, after_lifecycle,
                before_content_hash, after_content_hash, concurrency_token)
            VALUES (
                @ProfileId, @VersionId, @Action, @Actor, @Reason,
                @RequestId, @BeforeLifecycle, @AfterLifecycle,
                @BeforeHash, @AfterHash, @Token);
            """, new
        {
            ProfileId = profileId,
            VersionId = versionId,
            Action = action,
            Actor = actor,
            Reason = reason.Trim(),
            RequestId = requestId,
            BeforeLifecycle = beforeLifecycle,
            AfterLifecycle = afterLifecycle,
            BeforeHash = beforeHash,
            AfterHash = afterHash,
            Token = token
        }, transaction, cancellationToken: cancellationToken));

    private static object VersionParameters(long versionId,
        ApiDestinationProfileDraftRequest draft, string hash, string actor) => new
        {
            VersionId = versionId,
            draft.Environment,
            draft.SchemaBaseUrl,
            draft.SchemaPathTemplate,
            draft.DeliveryBaseUrl,
            draft.DeliveryPathTemplate,
            draft.Method,
            draft.AuthScheme,
            draft.AuthSecretReference,
            draft.NetworkPolicyReference,
            draft.TimeoutSeconds,
            draft.IdempotencyHeaderName,
            draft.CorrelationHeaderName,
            AllowedHeaderNames = draft.AllowedHeaderNames.ToArray(),
            ContentHash = hash,
            Actor = actor
        };

    private static ApiDestinationProfileDraftRequest ToDraft(
        ApiDestinationProfileVersionAdminResponse source) => new(
        source.Environment, source.SchemaBaseUrl!, source.SchemaPathTemplate,
        source.DeliveryBaseUrl!, source.DeliveryPathTemplate, source.Method,
        source.AuthScheme, source.AuthSecretReference,
        source.NetworkPolicyReference, source.TimeoutSeconds,
        source.IdempotencyHeaderName, source.CorrelationHeaderName,
        source.AllowedHeaderNames, source.SupportedContracts,
        source.PayloadBindings);

    private static ApiDestinationProfile ToDomain(
        ApiDestinationProfileAdminResponse profile,
        ApiDestinationProfileVersionAdminResponse version) => new(
        profile.ProfileKey, profile.DisplayName, true,
        version.SchemaBaseUrl!, version.SchemaPathTemplate,
        version.DeliveryBaseUrl!, version.DeliveryPathTemplate, version.Method,
        version.AllowedHeaderNames, version.AuthSecretReference ?? string.Empty,
        version.TimeoutSeconds, version.SupportedContracts,
        version.PayloadBindings, version.AuthScheme,
        version.IdempotencyHeaderName, version.CorrelationHeaderName,
        version.Environment, version.ProfileVersionId, version.VersionNo,
        version.Lifecycle, version.NetworkPolicyReference, version.ContentHash);

    private static string ContentHash(ApiDestinationProfileDraftRequest draft)
    {
        var profile = new ApiDestinationProfile(
            string.Empty,
            string.Empty,
            true,
            draft.SchemaBaseUrl,
            draft.SchemaPathTemplate,
            draft.DeliveryBaseUrl,
            draft.DeliveryPathTemplate,
            draft.Method,
            draft.AllowedHeaderNames,
            draft.AuthSecretReference ?? string.Empty,
            draft.TimeoutSeconds,
            draft.SupportedContracts,
            draft.PayloadBindings,
            draft.AuthScheme,
            draft.IdempotencyHeaderName,
            draft.CorrelationHeaderName,
            draft.Environment,
            NetworkPolicyReference: draft.NetworkPolicyReference);
        return ApiDestinationProfileCanonicalizer.ComputeContentHash(profile);
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 10 or > 500)
        {
            throw new ArgumentException(
                "Reason must contain between 10 and 500 characters.",
                nameof(reason));
        }
    }

    private static void ValidateProfileIdentity(
        string profileKey,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(profileKey) ||
            profileKey.Length > 100 ||
            profileKey.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '-' or '_' or '.')))
        {
            throw new ArgumentException(
                "Profile key must contain 1-100 ASCII letters, digits, '.', '-' or '_'.",
                nameof(profileKey));
        }
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200)
        {
            throw new ArgumentException(
                "Display name must contain 1-200 characters.",
                nameof(displayName));
        }
    }

    private sealed class ProfileRow
    {
        public long Id { get; init; }
        public string ProfileKey { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public long? ActiveVersionId { get; init; }
        public int? ActiveVersionNo { get; init; }
        public long ConcurrencyToken { get; init; }
    }

    private sealed class VersionListRow : VersionRow
    {
        public long ProfileId { get; init; }
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

    private sealed record VersionResponseRow(
        long ProfileId,
        ApiDestinationProfileVersionAdminResponse Response);

    private class VersionRow
    {
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
        public long ConcurrencyToken { get; init; }
        public DateTimeOffset? ValidatedAt { get; init; }
    }

    private sealed record VersionIdentity(long ProfileId, long VersionId,
        string ContentHash);
    private sealed record LifecycleRow(long Id, string Lifecycle,
        string ContentHash, string? ValidatedContentHash);
    private sealed record BindingRow(string TargetPath, string SourceText);

    private static VersionResponseRow ToResponse(
        VersionListRow row,
        IReadOnlyList<SupportedContract> contracts,
        IReadOnlyList<PayloadBinding> bindings) => new(
        row.ProfileId,
        new ApiDestinationProfileVersionAdminResponse(
            row.ProfileVersionId,
            row.VersionNo,
            row.Lifecycle,
            row.Environment,
            row.SchemaBaseUrl,
            row.SchemaPathTemplate,
            row.DeliveryBaseUrl,
            row.DeliveryPathTemplate,
            row.Method,
            row.AuthScheme,
            row.AuthSecretReference,
            row.NetworkPolicyReference,
            row.TimeoutSeconds,
            row.IdempotencyHeaderName,
            row.CorrelationHeaderName,
            row.AllowedHeaderNames,
            contracts,
            bindings,
            row.ContentHash,
            row.ConcurrencyToken,
            row.ValidatedAt));
}
