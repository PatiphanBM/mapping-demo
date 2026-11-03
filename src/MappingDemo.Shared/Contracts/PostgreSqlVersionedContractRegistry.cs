using System.Text;
using Dapper;
using Npgsql;

namespace MappingDemo.Shared.Contracts;

public sealed class PostgreSqlVersionedContractRegistry(
    NpgsqlDataSource dataSource,
    ContractSchemaModule? schemas = null) : IVersionedContractRegistrySource
{
    private readonly ContractSchemaModule _schemas =
        schemas ?? new ContractSchemaModule();

    public async ValueTask<IReadOnlyList<ContractVersionSummary>>
        ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        return await QueryListAsync(connection, null, cancellationToken);
    }

    public async ValueTask<IReadOnlyList<ContractVersionSummary>>
        ListPublishedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        return await QueryListAsync(
            connection,
            ContractVersionLifecycle.Published,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<ContractVersionSummary>>
        QueryListAsync(
            NpgsqlConnection connection,
            ContractVersionLifecycle? lifecycle,
            CancellationToken cancellationToken) =>
        (await connection.QueryAsync<ContractVersionSummary>(
            new CommandDefinition("""
                SELECT v.id AS "ContractVersionId",
                       c.contract_key AS "ContractKey",
                       c.display_name AS "DisplayName",
                       v.version AS "Version",
                       v.reference AS "Reference",
                       v.lifecycle AS "Lifecycle",
                       v.schema_hash AS "SchemaHash",
                       v.concurrency_token AS "ConcurrencyToken"
                FROM contract_versions v
                INNER JOIN contracts c ON c.id = v.contract_id
                WHERE @Lifecycle IS NULL OR v.lifecycle = @Lifecycle
                ORDER BY c.display_name, c.contract_key, v.created_at DESC,
                         v.id DESC;
                """, new { Lifecycle = lifecycle?.ToString() },
                cancellationToken: cancellationToken))).ToArray();

    public async ValueTask<ContractAggregateResult> GetAsync(
        string contractKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var contract = await connection.QuerySingleOrDefaultAsync<ContractRow>(
            new CommandDefinition("""
                SELECT id AS "ContractId", contract_key AS "ContractKey",
                       display_name AS "DisplayName",
                       concurrency_token AS "ConcurrencyToken"
                FROM contracts WHERE contract_key = @ContractKey;
                """, new { ContractKey = contractKey },
                cancellationToken: cancellationToken));
        if (contract is null)
        {
            return VersionedContractRegistryModule.AggregateFailure(
                "ContractNotFound",
                $"Contract '{contractKey}' does not exist.");
        }
        var versions = (await connection.QueryAsync<VersionRow>(
            new CommandDefinition(VersionSelectSql + "\n" + """
                WHERE v.contract_id = @ContractId
                ORDER BY v.created_at DESC, v.id DESC;
                """, new { contract.ContractId },
                cancellationToken: cancellationToken)))
            .Select(ToDetails)
            .ToArray();
        return new ContractAggregateResult(
            new ContractAggregate(
                contract.ContractId,
                contract.ContractKey,
                contract.DisplayName,
                contract.ConcurrencyToken,
                versions),
            []);
    }

    public async ValueTask<ContractVersionResult> ResolveExactAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<VersionRow>(
            new CommandDefinition(VersionSelectSql + "\n" + """
                WHERE v.reference = @Reference;
                """, new { Reference = reference },
                cancellationToken: cancellationToken));
        if (row is null)
        {
            return VersionedContractRegistryModule.VersionFailure(
                "ContractNotFound",
                $"Contract Version '{reference}' does not exist.");
        }
        if (row.Lifecycle == ContractVersionLifecycle.Draft)
        {
            return VersionedContractRegistryModule.VersionFailure(
                "ContractNotPublished",
                $"Contract Version '{reference}' is not published.");
        }
        var compilation = _schemas.Compile(row.CanonicalSchema!);
        if (!compilation.IsSuccess ||
            compilation.SchemaHash != row.SchemaHash)
        {
            return VersionedContractRegistryModule.VersionFailure(
                "ContractRegistryCorrupt",
                "Stored Contract canonical content does not match its hash.");
        }
        return new ContractVersionResult(
            new ResolvedContractVersion(
                ToDetails(row),
                compilation.TargetSchema!,
                compilation.PathCatalog),
            []);
    }

    public async ValueTask<ContractValidationResult> ValidateAsync(
        long contractVersionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await LoadVersionRowAsync(
            connection,
            contractVersionId,
            null,
            cancellationToken);
        if (row is null)
        {
            return ValidationFailure(
                "ContractNotFound",
                $"Contract Version '{contractVersionId}' does not exist.");
        }
        if (row.Lifecycle != ContractVersionLifecycle.Draft)
        {
            return ValidationFailure(
                "ContractDraftRequired",
                "Only a Draft Contract Version can be validated.");
        }
        return await CompileAndCompareAsync(
            connection,
            row,
            null,
            cancellationToken);
    }

    public async ValueTask<ContractMutationResult> ExecuteAsync(
        ContractRegistryCommand command,
        CancellationToken cancellationToken = default)
    {
        var commandDiagnostic = ValidateCommand(command);
        if (commandDiagnostic is not null)
        {
            return new ContractMutationResult(null, [commandDiagnostic]);
        }
        try
        {
            return command switch
            {
                CreateContractDraftCommand create =>
                    await CreateAsync(create, cancellationToken),
                UpdateContractDraftCommand update =>
                    await UpdateAsync(update, cancellationToken),
                CloneContractVersionCommand clone =>
                    await CloneAsync(clone, cancellationToken),
                PublishContractVersionCommand publish =>
                    await PublishAsync(publish, cancellationToken),
                RetireContractVersionCommand retire =>
                    await RetireAsync(retire, cancellationToken),
                _ => VersionedContractRegistryModule.MutationFailure(
                    "UnknownContractCommand",
                    "The Contract Registry command is not supported.")
            };
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return VersionedContractRegistryModule.MutationFailure(
                "ContractConflict",
                "The Contract or Version already exists or has a competing Draft.");
        }
        catch (PostgresException exception) when (
            exception.SqlState is PostgresErrorCodes.CheckViolation or
                PostgresErrorCodes.ForeignKeyViolation)
        {
            return VersionedContractRegistryModule.MutationFailure(
                "InvalidContractState",
                "The Contract mutation violates a registry invariant.");
        }
    }

    private async ValueTask<ContractMutationResult> CreateAsync(
        CreateContractDraftCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            BundledVersionedContractRegistry.ValidateIdentity(
                command.ContractKey,
                command.Version);
        }
        catch (ArgumentException)
        {
            return VersionedContractRegistryModule.MutationFailure(
                "InvalidContractIdentity",
                "Contract key or version is invalid.");
        }
        var schemaDiagnostic = ValidateDraftSchema(command.Schema);
        if (schemaDiagnostic is not null)
        {
            return new ContractMutationResult(null, [schemaDiagnostic]);
        }

        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var contract = await connection.QuerySingleOrDefaultAsync<ContractRow>(
            new CommandDefinition("""
                SELECT id AS "ContractId", contract_key AS "ContractKey",
                       display_name AS "DisplayName",
                       concurrency_token AS "ConcurrencyToken"
                FROM contracts WHERE contract_key = @ContractKey
                FOR UPDATE;
                """, new { command.ContractKey }, transaction,
                cancellationToken: cancellationToken));
        var action = "CreateDraft";
        if (contract is null)
        {
            var contractId = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition("""
                    INSERT INTO contracts (
                        contract_key, display_name, created_by, updated_by)
                    VALUES (
                        @ContractKey, @DisplayName, @Actor, @Actor)
                    RETURNING id;
                    """, command, transaction,
                    cancellationToken: cancellationToken));
            contract = new ContractRow(
                contractId,
                command.ContractKey,
                command.DisplayName,
                1);
            action = "CreateContract";
        }
        var reference = $"{command.ContractKey}/{command.Version}";
        var versionId = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition("""
                INSERT INTO contract_versions (
                    contract_id, version, reference, lifecycle,
                    draft_schema, created_by, updated_by)
                VALUES (
                    @ContractId, @Version, @Reference, 'Draft',
                    @Schema, @Actor, @Actor)
                RETURNING id;
                """, new
            {
                contract.ContractId,
                command.Version,
                Reference = reference,
                command.Schema,
                command.Actor
            }, transaction, cancellationToken: cancellationToken));
        await InsertAuditAsync(
            connection,
            transaction,
            contract.ContractId,
            versionId,
            action,
            command,
            null,
            "Draft",
            null,
            null,
            1,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadMutationResultAsync(
            connection,
            versionId,
            cancellationToken);
    }

    private async ValueTask<ContractMutationResult> UpdateAsync(
        UpdateContractDraftCommand command,
        CancellationToken cancellationToken)
    {
        var schemaDiagnostic = ValidateDraftSchema(command.Schema);
        if (schemaDiagnostic is not null)
        {
            return new ContractMutationResult(null, [schemaDiagnostic]);
        }
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var row = await LoadVersionRowAsync(
            connection,
            command.ContractVersionId,
            transaction,
            cancellationToken,
            forUpdate: true);
        if (row is null ||
            row.Lifecycle != ContractVersionLifecycle.Draft ||
            row.ConcurrencyToken != command.ExpectedConcurrencyToken)
        {
            return VersionedContractRegistryModule.MutationFailure(
                "ContractConcurrencyConflict",
                "The Draft is missing, immutable, or has changed.");
        }
        var nextToken = row.ConcurrencyToken + 1;
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE contract_versions
            SET draft_schema = @Schema,
                concurrency_token = @NextToken,
                updated_at = now(),
                updated_by = @Actor
            WHERE id = @VersionId;
            """, new
        {
            command.Schema,
            NextToken = nextToken,
            command.Actor,
            VersionId = row.ContractVersionId
        }, transaction, cancellationToken: cancellationToken));
        await InsertAuditAsync(
            connection,
            transaction,
            row.ContractId,
            row.ContractVersionId,
            "UpdateDraft",
            command,
            "Draft",
            "Draft",
            null,
            null,
            nextToken,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadMutationResultAsync(
            connection,
            row.ContractVersionId,
            cancellationToken);
    }

    private async ValueTask<ContractMutationResult> CloneAsync(
        CloneContractVersionCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var source = await LoadVersionRowAsync(
            connection,
            command.SourceContractVersionId,
            transaction,
            cancellationToken,
            forUpdate: true);
        if (source is null ||
            source.Lifecycle == ContractVersionLifecycle.Draft)
        {
            return VersionedContractRegistryModule.MutationFailure(
                "PublishedContractRequired",
                "Clone requires a Published or Retired Contract Version.");
        }
        try
        {
            BundledVersionedContractRegistry.ValidateIdentity(
                source.ContractKey,
                command.NewVersion);
        }
        catch (ArgumentException)
        {
            return VersionedContractRegistryModule.MutationFailure(
                "InvalidContractIdentity",
                "Contract version is invalid.");
        }
        var versionId = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition("""
                INSERT INTO contract_versions (
                    contract_id, version, reference, lifecycle,
                    draft_schema, created_by, updated_by)
                VALUES (
                    @ContractId, @Version, @Reference, 'Draft',
                    @Schema, @Actor, @Actor)
                RETURNING id;
                """, new
            {
                source.ContractId,
                Version = command.NewVersion,
                Reference = $"{source.ContractKey}/{command.NewVersion}",
                Schema = source.CanonicalSchema!,
                command.Actor
            }, transaction, cancellationToken: cancellationToken));
        await InsertAuditAsync(
            connection,
            transaction,
            source.ContractId,
            versionId,
            "CloneVersion",
            command,
            source.Lifecycle.ToString(),
            "Draft",
            source.SchemaHash,
            null,
            1,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadMutationResultAsync(
            connection,
            versionId,
            cancellationToken);
    }

    private async ValueTask<ContractMutationResult> PublishAsync(
        PublishContractVersionCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var row = await LoadVersionRowAsync(
            connection,
            command.ContractVersionId,
            transaction,
            cancellationToken,
            forUpdate: true);
        if (row is null ||
            row.Lifecycle != ContractVersionLifecycle.Draft ||
            row.ConcurrencyToken != command.ExpectedConcurrencyToken)
        {
            return VersionedContractRegistryModule.MutationFailure(
                "ContractConcurrencyConflict",
                "The Draft is missing, immutable, or has changed.");
        }
        var validation = await CompileAndCompareAsync(
            connection,
            row,
            transaction,
            cancellationToken);
        if (!validation.IsValid)
        {
            return new ContractMutationResult(null, validation.Diagnostics);
        }
        if (validation.Compatibility == ContractCompatibility.Breaking &&
            !command.AcknowledgeBreakingChange)
        {
            return VersionedContractRegistryModule.MutationFailure(
                "BreakingChangeAcknowledgementRequired",
                "Publishing this Contract Version requires explicit breaking-change acknowledgement.");
        }
        var nextToken = row.ConcurrencyToken + 1;
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE contract_versions
            SET lifecycle = 'Published',
                canonical_schema = @CanonicalSchema,
                schema_hash = @SchemaHash,
                compatibility = @Compatibility,
                compatibility_base_version_id = @BaseVersionId,
                concurrency_token = @NextToken,
                updated_at = now(),
                updated_by = @Actor,
                published_at = now(),
                published_by = @Actor
            WHERE id = @VersionId;
            """, new
        {
            validation.CanonicalSchema,
            validation.SchemaHash,
            Compatibility = validation.Compatibility!.Value.ToString(),
            BaseVersionId = validation.CompatibilityBaseVersionId,
            NextToken = nextToken,
            command.Actor,
            VersionId = row.ContractVersionId
        }, transaction, cancellationToken: cancellationToken));
        await InsertAuditAsync(
            connection,
            transaction,
            row.ContractId,
            row.ContractVersionId,
            "Publish",
            command,
            "Draft",
            "Published",
            null,
            validation.SchemaHash,
            nextToken,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadMutationResultAsync(
            connection,
            row.ContractVersionId,
            cancellationToken);
    }

    private async ValueTask<ContractMutationResult> RetireAsync(
        RetireContractVersionCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var row = await LoadVersionRowAsync(
            connection,
            command.ContractVersionId,
            transaction,
            cancellationToken,
            forUpdate: true);
        if (row is null ||
            row.Lifecycle != ContractVersionLifecycle.Published ||
            row.ConcurrencyToken != command.ExpectedConcurrencyToken)
        {
            return VersionedContractRegistryModule.MutationFailure(
                "ContractConcurrencyConflict",
                "The Published Version is missing, retired, or has changed.");
        }
        var activeProfileCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("""
                SELECT count(*)
                FROM api_destination_profile_contracts pc
                INNER JOIN api_destination_profile_versions pv
                    ON pv.id = pc.profile_version_id
                WHERE pc.contract_version_id = @ContractVersionId
                  AND pv.lifecycle = 'Active';
                """, new { row.ContractVersionId }, transaction,
                cancellationToken: cancellationToken));
        if (activeProfileCount > 0)
        {
            return VersionedContractRegistryModule.MutationFailure(
                "ContractVersionInUse",
                "Contract Version is attached to an Active API Destination Profile.");
        }
        var nextToken = row.ConcurrencyToken + 1;
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE contract_versions
            SET lifecycle = 'Retired',
                concurrency_token = @NextToken,
                updated_at = now(),
                updated_by = @Actor,
                retired_at = now(),
                retired_by = @Actor
            WHERE id = @VersionId;
            """, new
        {
            NextToken = nextToken,
            command.Actor,
            VersionId = row.ContractVersionId
        }, transaction, cancellationToken: cancellationToken));
        await InsertAuditAsync(
            connection,
            transaction,
            row.ContractId,
            row.ContractVersionId,
            "Retire",
            command,
            "Published",
            "Retired",
            row.SchemaHash,
            row.SchemaHash,
            nextToken,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadMutationResultAsync(
            connection,
            row.ContractVersionId,
            cancellationToken);
    }

    private async ValueTask<ContractValidationResult> CompileAndCompareAsync(
        NpgsqlConnection connection,
        VersionRow draft,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var compilation = _schemas.Compile(draft.DraftSchema);
        if (!compilation.IsSuccess)
        {
            return new ContractValidationResult(
                false,
                null,
                null,
                null,
                [],
                null,
                null,
                compilation.Diagnostics);
        }
        var baseline = await connection.QuerySingleOrDefaultAsync<BaselineRow>(
            new CommandDefinition("""
                SELECT id AS "ContractVersionId",
                       canonical_schema AS "CanonicalSchema"
                FROM contract_versions
                WHERE contract_id = @ContractId
                  AND id <> @DraftId
                  AND lifecycle IN ('Published', 'Retired')
                ORDER BY published_at DESC, id DESC
                LIMIT 1;
                """, new
            {
                draft.ContractId,
                DraftId = draft.ContractVersionId
            }, transaction, cancellationToken: cancellationToken));
        ContractSchemaCompilationResult? baselineCompilation = null;
        if (baseline is not null)
        {
            baselineCompilation = _schemas.Compile(baseline.CanonicalSchema);
            if (!baselineCompilation.IsSuccess)
            {
                return ValidationFailure(
                    "ContractRegistryCorrupt",
                    "The compatibility baseline cannot be compiled.");
            }
        }
        var compatibility = ContractCompatibilityAnalyzer.Compare(
            baselineCompilation?.PathCatalog,
            compilation.PathCatalog);
        return new ContractValidationResult(
            true,
            compilation.CanonicalSchema,
            compilation.SchemaHash,
            compilation.TargetSchema,
            compilation.PathCatalog,
            compatibility,
            baseline?.ContractVersionId,
            []);
    }

    private static ContractDiagnostic? ValidateCommand(
        ContractRegistryCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Actor))
        {
            return new("actor", "ActorRequired", "Actor is required.");
        }
        if (string.IsNullOrWhiteSpace(command.RequestId))
        {
            return new(
                "requestId",
                "RequestIdRequired",
                "Request ID is required.");
        }
        if (string.IsNullOrWhiteSpace(command.Reason) ||
            command.Reason.Trim().Length is < 10 or > 500)
        {
            return new(
                "reason",
                "InvalidReason",
                "Reason must contain between 10 and 500 characters.");
        }
        return null;
    }

    private static ContractDiagnostic? ValidateDraftSchema(string schema)
    {
        var bytes = Encoding.UTF8.GetByteCount(schema ?? string.Empty);
        if (bytes is < 2 or > ContractRegistryModule.MaxSchemaBytes)
        {
            return new ContractDiagnostic(
                "schema",
                "InvalidSchemaSize",
                "Contract schema must contain 2 bytes to 1 MiB.");
        }
        return ContractSchemaContentPolicy.Validate(schema!);
    }

    private static ContractValidationResult ValidationFailure(
        string code,
        string message) => new(
        false,
        null,
        null,
        null,
        [],
        null,
        null,
        [new ContractDiagnostic("contract", code, message)]);

    private async ValueTask<ContractMutationResult> LoadMutationResultAsync(
        NpgsqlConnection connection,
        long versionId,
        CancellationToken cancellationToken)
    {
        var row = await LoadVersionRowAsync(
            connection,
            versionId,
            null,
            cancellationToken);
        return row is null
            ? VersionedContractRegistryModule.MutationFailure(
                "ContractNotFound",
                "The resulting Contract Version was not found.")
            : new ContractMutationResult(ToDetails(row), []);
    }

    private static async Task InsertAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long contractId,
        long contractVersionId,
        string action,
        ContractRegistryCommand command,
        string? beforeLifecycle,
        string? afterLifecycle,
        string? beforeHash,
        string? afterHash,
        long token,
        CancellationToken cancellationToken) =>
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO contract_audit (
                contract_id, contract_version_id, action, actor, reason,
                request_id, before_lifecycle, after_lifecycle,
                before_schema_hash, after_schema_hash, concurrency_token)
            VALUES (
                @ContractId, @ContractVersionId, @Action, @Actor, @Reason,
                @RequestId, @BeforeLifecycle, @AfterLifecycle,
                @BeforeHash, @AfterHash, @Token);
            """, new
        {
            ContractId = contractId,
            ContractVersionId = contractVersionId,
            Action = action,
            command.Actor,
            Reason = command.Reason.Trim(),
            command.RequestId,
            BeforeLifecycle = beforeLifecycle,
            AfterLifecycle = afterLifecycle,
            BeforeHash = beforeHash,
            AfterHash = afterHash,
            Token = token
        }, transaction, cancellationToken: cancellationToken));

    private static async Task<VersionRow?> LoadVersionRowAsync(
        NpgsqlConnection connection,
        long versionId,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken,
        bool forUpdate = false) =>
        await connection.QuerySingleOrDefaultAsync<VersionRow>(
            new CommandDefinition(
                VersionSelectSql + "\n" + """
                WHERE v.id = @VersionId
                """ + (forUpdate ? " FOR UPDATE;" : ";"),
                new { VersionId = versionId },
                transaction,
                cancellationToken: cancellationToken));

    private static ContractVersionDetails ToDetails(VersionRow row) => new(
        row.ContractVersionId,
        row.ContractId,
        row.ContractKey,
        row.DisplayName,
        row.Version,
        row.Reference,
        row.Lifecycle,
        row.DraftSchema,
        row.CanonicalSchema,
        row.SchemaHash,
        row.Compatibility,
        row.CompatibilityBaseVersionId,
        row.ConcurrencyToken,
        row.CreatedAt,
        row.PublishedAt,
        row.RetiredAt);

    private const string VersionSelectSql = """
        SELECT v.id AS "ContractVersionId",
               v.contract_id AS "ContractId",
               c.contract_key AS "ContractKey",
               c.display_name AS "DisplayName",
               v.version AS "Version",
               v.reference AS "Reference",
               v.lifecycle AS "Lifecycle",
               v.draft_schema AS "DraftSchema",
               v.canonical_schema AS "CanonicalSchema",
               v.schema_hash AS "SchemaHash",
               v.compatibility AS "Compatibility",
               v.compatibility_base_version_id AS "CompatibilityBaseVersionId",
               v.concurrency_token AS "ConcurrencyToken",
               v.created_at AS "CreatedAt",
               v.published_at AS "PublishedAt",
               v.retired_at AS "RetiredAt"
        FROM contract_versions v
        INNER JOIN contracts c ON c.id = v.contract_id
        """;

    private sealed record ContractRow(
        long ContractId,
        string ContractKey,
        string DisplayName,
        long ConcurrencyToken);

    private sealed class VersionRow
    {
        public long ContractVersionId { get; init; }
        public long ContractId { get; init; }
        public string ContractKey { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string Reference { get; init; } = string.Empty;
        public ContractVersionLifecycle Lifecycle { get; init; }
        public string DraftSchema { get; init; } = string.Empty;
        public string? CanonicalSchema { get; init; }
        public string? SchemaHash { get; init; }
        public ContractCompatibility? Compatibility { get; init; }
        public long? CompatibilityBaseVersionId { get; init; }
        public long ConcurrencyToken { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? PublishedAt { get; init; }
        public DateTimeOffset? RetiredAt { get; init; }
    }

    private sealed record BaselineRow(
        long ContractVersionId,
        string CanonicalSchema);
}
