namespace MappingDemo.Shared.Jobs;

public static class ImportStatus
{
    public const string Queued = "Queued";

    public const string Delaying = "Delaying";

    public const string Importing = "Importing";

    public const string Imported = "Imported";

    public const string ImportFailed = "ImportFailed";

    public const string Duplicate = "Duplicate";
}
