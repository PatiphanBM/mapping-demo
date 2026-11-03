using Dapper;
using MappingDemo.Shared.Contracts;
using Npgsql;

namespace MappingDemo.Api.Services;

public sealed class ContractRegistryBootstrapper(
    IVersionedContractRegistry registry,
    NpgsqlDataSource dataSource,
    IHostEnvironment environment,
    ContractSchemaModule schemas)
{
    private const string Actor = "phase-27.5-bootstrap";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        foreach (var definition in
            BundledVersionedContractRegistryFactory.LoadDefinitions(environment))
        {
            await EnsureDefinitionAsync(definition, cancellationToken);
        }

        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE api_destination_profile_contracts pc
            SET contract_version_id = v.id
            FROM contract_versions v
            INNER JOIN contracts c ON c.id = v.contract_id
            WHERE pc.contract_version_id IS NULL
              AND v.lifecycle = 'Published'
              AND pc.contract_id = c.contract_key
              AND pc.contract_version = v.version
              AND pc.contract_reference = v.reference;
            """, cancellationToken: cancellationToken));
    }

    private async Task EnsureDefinitionAsync(
        BundledContractDefinition definition,
        CancellationToken cancellationToken)
    {
        var compilation = schemas.Compile(definition.Schema);
        if (!compilation.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Bootstrap Contract '{definition.ContractKey}/{definition.Version}' is invalid.");
        }

        var aggregate = await registry.GetAsync(
            definition.ContractKey,
            cancellationToken);
        ContractVersionDetails version;
        if (!aggregate.IsSuccess)
        {
            var create = await registry.ExecuteAsync(
                new CreateContractDraftCommand(
                    definition.ContractKey,
                    definition.DisplayName,
                    definition.Version,
                    definition.Schema,
                    Actor,
                    "Seed bundled Contract into PostgreSQL registry",
                    $"phase-27.5-create-{definition.ContractKey}-{definition.Version}"),
                cancellationToken);
            version = RequireVersion(create, definition);
        }
        else
        {
            version = aggregate.Contract!.Versions.SingleOrDefault(item =>
                    item.Version == definition.Version)
                ?? throw new InvalidOperationException(
                    $"Contract '{definition.ContractKey}' exists without required Version '{definition.Version}'.");
        }

        if (version.Lifecycle == ContractVersionLifecycle.Draft)
        {
            var draftCompilation = schemas.Compile(version.DraftSchema);
            if (!draftCompilation.IsSuccess ||
                draftCompilation.SchemaHash != compilation.SchemaHash)
            {
                throw new InvalidOperationException(
                    $"Bootstrap Contract '{version.Reference}' conflicts with an existing Draft.");
            }
            var publish = await registry.ExecuteAsync(
                new PublishContractVersionCommand(
                    version.ContractVersionId,
                    version.ConcurrencyToken,
                    false,
                    Actor,
                    "Publish seeded bundled Contract after parity check",
                    $"phase-27.5-publish-{definition.ContractKey}-{definition.Version}"),
                cancellationToken);
            version = RequireVersion(publish, definition);
        }

        if (version.Lifecycle != ContractVersionLifecycle.Published ||
            version.SchemaHash != compilation.SchemaHash ||
            version.CanonicalSchema != compilation.CanonicalSchema)
        {
            throw new InvalidOperationException(
                $"PostgreSQL Contract '{version.Reference}' does not match the bundled bootstrap input.");
        }

        var resolved = await registry.ResolveExactAsync(
            version.Reference,
            cancellationToken);
        if (!resolved.IsSuccess ||
            !resolved.ContractVersion!.PathCatalog.SequenceEqual(
                compilation.PathCatalog))
        {
            throw new InvalidOperationException(
                $"PostgreSQL Contract '{version.Reference}' path catalog does not match bootstrap input.");
        }
    }

    private static ContractVersionDetails RequireVersion(
        ContractMutationResult result,
        BundledContractDefinition definition) =>
        result.Version ?? throw new InvalidOperationException(
            $"Could not seed Contract '{definition.ContractKey}/{definition.Version}': " +
            string.Join(
                "; ",
                result.Diagnostics.Select(item =>
                    $"{item.Code}: {item.Message}")));
}
