using MappingDemo.Api.Contracts.DatabaseDestinations;
using MappingDemo.Shared.DatabaseDestinations;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("database-destinations")]
public sealed class DatabaseDestinationsController(
    IDatabaseDestinationProfileCatalog profiles,
    IDatabaseTargetSchemaInspector inspector) : ControllerBase
{
    [HttpGet("profiles")]
    public ActionResult<IReadOnlyList<DatabaseDestinationProfileResponse>>
        GetProfiles()
    {
        return Ok(profiles.GetAll().Select(profile =>
            new DatabaseDestinationProfileResponse(
                profile.Id,
                profile.Provider,
                profile.AllowedSchemas,
                profile.WriteEnabled)).ToArray());
    }

    [HttpGet("profiles/{profileId}/schemas")]
    public IActionResult GetSchemas(string profileId)
    {
        try
        {
            return Ok(profiles.GetRequired(profileId).AllowedSchemas);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("profiles/{profileId}/schemas/{schema}/tables")]
    public async Task<IActionResult> GetTables(
        string profileId,
        string schema,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await inspector.ListTablesAsync(
                profileId,
                schema,
                cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DatabaseSchemaInspectionException exception)
        {
            return InspectionProblem(exception);
        }
    }

    [HttpGet("profiles/{profileId}/schemas/{schema}/tables/{table}")]
    public async Task<IActionResult> Inspect(
        string profileId,
        string schema,
        string table,
        [FromQuery] bool refresh,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await inspector.InspectAsync(
                new TargetTableReference(profileId, schema, table),
                refresh,
                cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DatabaseSchemaInspectionException exception)
        {
            return InspectionProblem(exception);
        }
    }

    private ObjectResult InspectionProblem(
        DatabaseSchemaInspectionException exception)
    {
        var status = exception.Code is "SchemaNotAllowed"
            ? StatusCodes.Status403Forbidden
            : exception.Code is "TargetTableNotFound"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status503ServiceUnavailable;
        return Problem(
            statusCode: status,
            title: exception.Code,
            detail: exception.Message);
    }
}
