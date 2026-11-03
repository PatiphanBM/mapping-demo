using MappingDemo.Shared.Contracts;

namespace MappingDemo.Tests.Contracts;

public sealed class ApiDestinationProfileCatalogTests
{
    [Fact]
    public void Catalog_exposes_safe_metadata_without_server_urls_or_secret_references()
    {
        var profile = ContractTestData.Profile("pa-mtls", "proposal/1");
        var catalog = new ApiDestinationProfileCatalog([profile]);

        var safe = Assert.Single(catalog.GetSafeProfiles());

        Assert.Equal("pa-mtls", safe.Id);
        Assert.Equal("POST", safe.Method);
        Assert.Equal("/proposals/{contractVersion}", safe.DeliveryPathTemplate);
        Assert.Equal(["X-Correlation-Id"], safe.AllowedHeaderNames);
        Assert.DoesNotContain("example.test", safe.ToString());
        Assert.DoesNotContain("secret", safe.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ftp://contracts.example.test", "InvalidSchemaBaseUrl")]
    [InlineData("https://127.0.0.1", "InvalidProfileHost")]
    [InlineData("https://contracts.example.test:8443", "ProfilePortNotAllowed")]
    [InlineData("https://contracts.example.test/path?token=x", "InvalidProfileUrl")]
    public void Catalog_rejects_unsafe_profile_urls(string schemaUrl, string code)
    {
        var profile = ContractTestData.Profile("unsafe", "proposal/1") with
        {
            SchemaBaseUrl = schemaUrl
        };

        var exception = Assert.Throws<ApiDestinationProfileValidationException>(
            () => new ApiDestinationProfileCatalog([profile]));

        Assert.Contains(exception.Diagnostics, item => item.Code == code);
    }

    [Theory]
    [InlineData("/deliveries/{unknown}", "InvalidPathTemplate")]
    [InlineData("/deliveries/{contractVersion}\\suffix", "InvalidPathTemplate")]
    public void Catalog_rejects_unsafe_delivery_templates(
        string template,
        string code)
    {
        var profile = ContractTestData.Profile("unsafe", "proposal/1") with
        {
            DeliveryPathTemplate = template
        };

        var exception = Assert.Throws<ApiDestinationProfileValidationException>(
            () => new ApiDestinationProfileCatalog([profile]));

        Assert.Contains(exception.Diagnostics, item => item.Code == code);
    }

    [Theory]
    [InlineData("Host")]
    [InlineData("Cookie")]
    [InlineData("Content-Length")]
    public void Catalog_rejects_forbidden_headers(string header)
    {
        var profile = ContractTestData.Profile("unsafe", "proposal/1") with
        {
            AllowedHeaderNames = [header]
        };

        var exception = Assert.Throws<ApiDestinationProfileValidationException>(
            () => new ApiDestinationProfileCatalog([profile]));

        Assert.Contains(exception.Diagnostics, item =>
            item.Code == "InvalidAllowedHeader");
    }

    [Fact]
    public void Catalog_rejects_case_insensitive_duplicate_headers()
    {
        var profile = ContractTestData.Profile("unsafe", "proposal/1") with
        {
            AllowedHeaderNames = ["X-Request-Id", "x-request-id"]
        };

        var exception = Assert.Throws<ApiDestinationProfileValidationException>(
            () => new ApiDestinationProfileCatalog([profile]));

        Assert.Contains(exception.Diagnostics, item =>
            item.Code == "DuplicateAllowedHeader");
    }

    [Fact]
    public void Catalog_rejects_unsafe_template_header_and_auth_reference()
    {
        var profile = ContractTestData.Profile("unsafe", "proposal/1") with
        {
            DeliveryPathTemplate = "/../deliveries/{contractVersion}",
            AllowedHeaderNames = ["Authorization"],
            AuthScheme = "Mtls",
            AuthSecretReference = string.Empty
        };

        var exception = Assert.Throws<ApiDestinationProfileValidationException>(
            () => new ApiDestinationProfileCatalog([profile]));

        Assert.Contains(exception.Diagnostics, item =>
            item.Code == "InvalidPathTemplate");
        Assert.Contains(exception.Diagnostics, item =>
            item.Code == "InvalidAllowedHeader");
        Assert.Contains(exception.Diagnostics, item =>
            item.Code == "InvalidAuthReference");
    }

    [Fact]
    public void Disabled_profile_cannot_be_resolved_for_creation()
    {
        var profile = ContractTestData.Profile("disabled", "proposal/1") with
        {
            Enabled = false
        };
        var catalog = new ApiDestinationProfileCatalog([profile]);

        var result = catalog.ResolveForCreation(profile.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("ProfileDisabled", Assert.Single(result.Diagnostics).Code);
    }
}

internal static class ContractTestData
{
    public static ApiDestinationProfile Profile(string id, string contractReference) =>
        new(
            id,
            id,
            true,
            "https://contracts.example.test",
            "/schemas/{contractReference}",
            "https://delivery.example.test",
            "/proposals/{contractVersion}",
            "POST",
            ["X-Correlation-Id"],
            string.Empty,
            10,
            [new SupportedContract(
                contractReference.Split('/')[0],
                contractReference.Split('/')[1],
                contractReference)],
            []);
}
