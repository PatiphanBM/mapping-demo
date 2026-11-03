namespace MappingDemo.Shared.Destinations;

public static class JsonArtifactLimits
{
    public const int MaxPreviewBytes = 1024 * 1024;

    public const int MaxPreviewRecords = 50;

    public const int MaxObjectBytes = 1024 * 1024;

    public const int MaxObjectsPerArtifact = 320_000;

    public const long MaxArtifactBytes = 256L * 1024 * 1024;
}
