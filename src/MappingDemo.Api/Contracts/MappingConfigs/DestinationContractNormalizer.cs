using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.MappingConfigs;

namespace MappingDemo.Api.Contracts.MappingConfigs;

public static class DestinationContractNormalizer
{
    public static DestinationNormalizationResult Normalize(
        DestinationDefinition? destination,
        long? legacyNormalizedTableId)
    {
        if (destination is null)
        {
            return legacyNormalizedTableId is > 0
                ? DestinationNormalizationResult.Success(
                    new LegacyTableDestination(legacyNormalizedTableId.Value))
                : DestinationNormalizationResult.Failure(
                    "destination",
                    "Destination is required when normalizedTableId is absent.");
        }

        if (destination is ApiContractDestination or JsonFileDestination
                or DatabaseTableDestination
            && legacyNormalizedTableId.HasValue)
        {
            return DestinationNormalizationResult.Failure(
                "destination",
                $"{destination.GetType().Name.Replace("Destination", string.Empty)} " +
                "destination cannot be combined with normalizedTableId.");
        }

        if (destination is LegacyTableDestination legacy
            && legacy.NormalizedTableId <= 0)
        {
            return DestinationNormalizationResult.Failure(
                "destination.normalizedTableId",
                "Normalized table ID must be a positive integer.");
        }

        if (destination is LegacyTableDestination explicitLegacy
            && legacyNormalizedTableId.HasValue
            && legacyNormalizedTableId.Value != explicitLegacy.NormalizedTableId)
        {
            return DestinationNormalizationResult.Failure(
                "destination.normalizedTableId",
                "Destination and normalizedTableId must identify the same table.");
        }

        var validationError = DestinationDefinitionValidator
            .Validate(destination)
            .FirstOrDefault();
        if (validationError is not null)
        {
            return DestinationNormalizationResult.Failure(
                validationError.Field,
                validationError.Message);
        }

        return DestinationNormalizationResult.Success(destination);
    }
}

public sealed record DestinationNormalizationResult(
    DestinationDefinition? Destination,
    string? ErrorPath,
    string? ErrorMessage)
{
    public bool IsSuccess => Destination is not null;

    public static DestinationNormalizationResult Success(
        DestinationDefinition destination) => new(destination, null, null);

    public static DestinationNormalizationResult Failure(
        string path,
        string message) => new(null, path, message);
}
