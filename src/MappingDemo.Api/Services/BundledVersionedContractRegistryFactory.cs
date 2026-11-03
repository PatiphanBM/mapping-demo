using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Services;

public static class BundledVersionedContractRegistryFactory
{
    public static BundledVersionedContractRegistry Create(
        IHostEnvironment environment) => new(
        LoadDefinitions(environment));

    public static IReadOnlyList<BundledContractDefinition> LoadDefinitions(
        IHostEnvironment environment)
    {
        var root = Path.Combine(
            environment.ContentRootPath,
            "ContractFixtures");
        return
        [
            Definition(
                root,
                "proposal",
                "Proposal",
                "1",
                "pa-proposal-v1.json"),
            Definition(
                root,
                "claims",
                "Claims",
                "2",
                "claims-envelope-v2.json")
        ];
    }

    private static BundledContractDefinition Definition(
        string root,
        string contractKey,
        string displayName,
        string version,
        string fileName)
    {
        var path = Path.Combine(root, fileName);
        return new BundledContractDefinition(
            contractKey,
            displayName,
            version,
            File.ReadAllText(path),
            new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
    }
}
