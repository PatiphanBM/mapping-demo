namespace MappingDemo.Shared.Normalization;

public sealed record RowNormalizationError(string Field, string Reason);
