namespace MappingDemo.Shared.Tables;

public sealed record TableValidationError(
    string Field,
    string Message);
