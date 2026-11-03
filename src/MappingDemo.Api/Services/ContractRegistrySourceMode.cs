namespace MappingDemo.Api.Services;

public enum ContractRegistrySourceMode
{
    Bundled,
    Postgres
}

public static class ContractRegistrySourceModeParser
{
    public static ContractRegistrySourceMode Parse(string? value) =>
        Enum.TryParse<ContractRegistrySourceMode>(
            value ?? nameof(ContractRegistrySourceMode.Postgres),
            ignoreCase: true,
            out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                "ContractRegistry:SourceMode must be Bundled or Postgres.");
}
