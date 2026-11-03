using MappingDemo.Shared.Contracts;

namespace MappingDemo.Tests.Contracts;

public sealed class FileDestinationProfileCatalogTests
{
    [Fact]
    public void Catalog_resolves_enabled_profile_without_endpoint_or_auth()
    {
        var profile = Profile("local-json-export", "proposal/1");
        var catalog = new FileDestinationProfileCatalog([profile]);

        var result = catalog.ResolveForCreation(profile.Id);
        var metadata = Assert.Single(catalog.GetSafeProfiles());

        Assert.True(result.IsSuccess);
        Assert.Same(profile, result.Profile);
        Assert.Equal(profile.Id, metadata.Id);
        Assert.Equal(profile.SupportedContracts, metadata.SupportedContracts);
        Assert.DoesNotContain(profile.OutputRoot, metadata.ToString());
        Assert.DoesNotContain("Root", metadata.ToString());
        Assert.DoesNotContain("Encryption", metadata.ToString());
    }

    [Fact]
    public void Disabled_profile_cannot_be_resolved_for_creation()
    {
        var profile = Profile("disabled-json", "proposal/1") with
        {
            Enabled = false
        };
        var catalog = new FileDestinationProfileCatalog([profile]);

        var result = catalog.ResolveForCreation(profile.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("ProfileDisabled", Assert.Single(result.Diagnostics).Code);
    }

    public static FileDestinationProfile Profile(
        string id,
        string contractReference) =>
        new(
            id,
            Enabled: true,
            OutputRoot: Path.GetFullPath(Path.Combine("output", id)),
            EncryptionPolicy: "volume-encrypted",
            QuotaBytes: 1024L * 1024 * 1024,
            MinimumFreeBytes: 512L * 1024 * 1024,
            UpdatedAt: DateTimeOffset.Parse("2026-10-21T00:00:00Z"),
            TimeoutSeconds: 10,
            SupportedContracts:
            [
                new SupportedContract(
                    contractReference.Split('/')[0],
                    contractReference.Split('/')[1],
                    contractReference)
            ],
            PayloadBindings: []);
}
