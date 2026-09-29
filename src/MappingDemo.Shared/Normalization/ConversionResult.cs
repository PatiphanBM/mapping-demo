namespace MappingDemo.Shared.Normalization;

public abstract record ConversionResult
{
    private ConversionResult()
    {
    }

    public sealed record Success(object? Value) : ConversionResult;

    public sealed record Failure(string Reason) : ConversionResult;
}
