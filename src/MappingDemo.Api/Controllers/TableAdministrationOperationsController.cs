using MappingDemo.Api.Services;
using MappingDemo.Shared.Tables;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("admin/table-operations")]
public sealed class TableAdministrationOperationsController(
    TableAdministrationObservability observability,
    ITableAdministrationAuthorizer authorizer) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var actor = User.Identity?.IsAuthenticated == true
            ? User.Identity.Name
            : Request.Headers["X-Operator-Id"].FirstOrDefault();
        if (!authorizer.IsAuthorized(
            User,
            "TableAdmin.Read",
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
