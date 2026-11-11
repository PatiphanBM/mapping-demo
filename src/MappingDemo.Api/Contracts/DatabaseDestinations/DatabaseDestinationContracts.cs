using MappingDemo.Shared.DatabaseDestinations;

namespace MappingDemo.Api.Contracts.DatabaseDestinations;

public sealed record DatabaseDestinationProfileResponse(
    string Id,
    DatabaseProvider Provider,
    IReadOnlyList<string> AllowedSchemas,
    bool WriteEnabled);
