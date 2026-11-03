using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("api-destination-profiles")]
public sealed class ApiDestinationProfilesController(
    IApiDestinationProfileCatalog profiles,
    ContractRegistryModule registry) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProfiles(
        CancellationToken cancellationToken) =>
        Ok(await profiles.GetSafeProfilesAsync(cancellationToken));

    [HttpPost("{profileId}/resolve")]
    public async Task<IActionResult> Resolve(
        string profileId,
        ContractReference contract,
        CancellationToken cancellationToken)
    {
        var result = await registry.ResolveAsync(profileId, contract, cancellationToken);
        if (!result.IsSuccess)
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                ModelState.AddModelError(diagnostic.Path, $"{diagnostic.Code}: {diagnostic.Message}");
            }
            return ValidationProblem(ModelState);
        }

        return Ok(new ContractResolveResponse(
            result.Contract!,
            result.SchemaHash!,
            result.FetchedAt!.Value,
            result.PathCatalog,
            new ContractRequestSummary(
                result.Profile!.Method,
                result.Profile.DeliveryPathTemplate,
                result.Profile.AllowedHeaderNames)));
    }
}

public sealed record ContractResolveResponse(
    ContractReference Contract,
    string SchemaHash,
    DateTimeOffset FetchedAt,
    IReadOnlyList<ContractPathDescriptor> Paths,
    ContractRequestSummary RequestSummary);

public sealed record ContractRequestSummary(
    string Method,
    string PathTemplate,
    IReadOnlyList<string> AllowedHeaderNames);
