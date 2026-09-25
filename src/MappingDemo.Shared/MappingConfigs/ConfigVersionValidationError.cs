namespace MappingDemo.Shared.MappingConfigs;

public sealed record ConfigVersionValidationError(
    string Field,
    string Message);
