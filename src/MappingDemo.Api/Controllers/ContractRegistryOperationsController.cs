using MappingDemo.Api.Services;
using MappingDemo.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("admin/contract-registry-operations")]
public sealed class ContractRegistryOperationsController(
    ContractRegistryObservability observability,
    IContractRegistryAuthorizer authorizer) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var actor = User.Identity?.IsAuthenticated == true
            ? User.Identity.Name
            : Request.Headers["X-Operator-Id"].FirstOrDefault();
        if (!authorizer.IsAuthorized(
            User,
            "ContractAdmin.Read",
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
