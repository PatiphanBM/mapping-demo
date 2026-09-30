namespace MappingDemo.Api.Services;

public enum FileReprocessOutcome
{
    Accepted,
    FileJobNotFound,
    InvalidVersion,
    Conflict
}

public sealed record FileReprocessResult(
    FileReprocessOutcome Outcome,
    int RowJobsCreated = 0,
    string? Error = null);
