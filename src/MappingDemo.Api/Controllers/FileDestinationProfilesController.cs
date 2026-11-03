using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Transformation;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("file-destination-profiles")]
public sealed class FileDestinationProfilesController(
    FileDestinationProfileCatalog profiles,
    ContractRegistryModule registry) : ControllerBase
{
    [HttpGet]
    public IActionResult GetProfiles() => Ok(profiles.GetSafeProfiles());

    [HttpPost("{profileId}/resolve")]
    public async Task<IActionResult> Resolve(
        string profileId,
        ContractReference contract,
        CancellationToken cancellationToken)
    {
        var result = await registry.ResolveFileAsync(
            profileId,
            contract,
            cancellationToken);
        if (!result.IsSuccess)
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                ModelState.AddModelError(
                    diagnostic.Path,
                    $"{diagnostic.Code}: {diagnostic.Message}");
            }
            return ValidationProblem(ModelState);
        }

        return Ok(new FileContractResolveResponse(
            result.Contract!,
            result.SchemaHash!,
            result.FetchedAt!.Value,
            result.PathCatalog));
    }
}

public sealed record FileContractResolveResponse(
    ContractReference Contract,
    string SchemaHash,
    DateTimeOffset FetchedAt,
    IReadOnlyList<ContractPathDescriptor> Paths);
