using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("admin/api-destination-profile-operations")]
public sealed class ApiDestinationProfileOperationsController(
    ApiDestinationProfileObservability observability,
    IProfileAdministrationAuthorizer authorizer) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var actor = User.Identity?.IsAuthenticated == true
            ? User.Identity.Name
            : Request.Headers["X-Operator-Id"].FirstOrDefault();
        if (!authorizer.IsAuthorized(
            User,
            "ProfileAdmin.Read",
            actor,
            mutation: false))
        {
            return User.Identity?.IsAuthenticated == true
                ? Forbid()
                : Unauthorized();
        }
        return Ok(observability.Snapshot());
    }
}
