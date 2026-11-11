using MappingDemo.Api.Contracts.DeliveryJobs;
using MappingDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("delivery-operations")]
public sealed class DeliveryOperationsController(
    DeliveryOperationsService operations,
    IDeliveryOperationsAuthorizer authorizer) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await operations.GetAsync(cancellationToken));

    [HttpPut("control")]
    public async Task<IActionResult> UpdateControl(
        UpdateDeliveryControlRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryAuthorize(request.Reason, out var operatorId, out var error))
        {
            return error!;
        }

        return Ok(await operations.UpdateControlAsync(
            request.DeliveryPaused,
            request.ApiContractActivationEnabled,
            request.JsonFileActivationEnabled,
            request.JsonFileFinalizerPaused,
            request.DatabaseTableActivationEnabled,
            request.DatabaseDeliveryPaused,
            operatorId!,
            request.Reason.Trim(),
            cancellationToken));
    }

    [HttpPut("rollouts/{configId:long}")]
    public async Task<IActionResult> UpdateRollout(
        long configId,
        UpdateDeliveryRolloutRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Stage is not ("Shadow" or "Canary" or "Expanded"))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Stage must be Shadow, Canary or Expanded."
            });
        }
        if (!TryAuthorize(request.Reason, out var operatorId, out var error))
        {
            return error!;
        }

        var updated = await operations.UpdateRolloutAsync(
            configId,
            request.Stage,
            request.Enabled,
            operatorId!,
            request.Reason.Trim(),
            cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    private bool TryAuthorize(
        string? reason,
        out string? operatorId,
        out IActionResult? error)
    {
        operatorId = Request.Headers["X-Operator-Id"].FirstOrDefault();
        if (!authorizer.IsAuthorized(User, operatorId))
        {
            error = Forbid();
            return false;
        }
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            error = BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "A reason between 1 and 500 characters is required."
            });
            return false;
        }

        error = null;
        return true;
    }
}
