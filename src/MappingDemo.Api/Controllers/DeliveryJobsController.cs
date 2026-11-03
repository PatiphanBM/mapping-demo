using MappingDemo.Api.Contracts.DeliveryJobs;
using MappingDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("delivery-jobs")]
public sealed class DeliveryJobsController(
    DeliveryJobService deliveryJobs,
    IDeliveryResendAuthorizer resendAuthorizer) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetForFileJob(
        [FromQuery] long fileJobId,
        CancellationToken cancellationToken)
    {
        if (fileJobId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "fileJobId must be greater than zero."
            });
        }

        return Ok(await deliveryJobs.GetForFileJobAsync(
            fileJobId,
            cancellationToken));
    }

    [HttpPost("{id:long}/retry")]
    public async Task<IActionResult> Retry(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await deliveryJobs.RetryAsync(id, cancellationToken);
        return ToActionResult(result, "Delivery job cannot be retried.");
    }

    [HttpPost("{id:long}/resend")]
    public async Task<IActionResult> Resend(
        long id,
        ResendDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        var operatorId = Request.Headers["X-Operator-Id"].FirstOrDefault();
        if (!resendAuthorizer.IsAuthorized(User, operatorId))
        {
            return Forbid();
        }

        var actionIdText = Request.Headers["X-Action-Id"].FirstOrDefault();
        if (!Guid.TryParse(actionIdText, out var actionId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "X-Action-Id must be a GUID for resend idempotency."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Length > 500)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "A resend reason between 1 and 500 characters is required."
            });
        }

        var result = await deliveryJobs.ResendAsync(
            id,
            actionId.ToString("D"),
            operatorId!,
            request.Reason.Trim(),
            cancellationToken);
        return ToActionResult(result, "Only a Delivered job can be resent.");
    }

    private IActionResult ToActionResult(
        DeliveryActionResult result,
        string conflictTitle)
    {
        return result.Outcome switch
        {
            DeliveryActionOutcome.Accepted => Accepted(new
            {
                result.DeliveryJobId
            }),
            DeliveryActionOutcome.NotFound => NotFound(),
            DeliveryActionOutcome.Conflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = conflictTitle
            }),
            _ => throw new InvalidOperationException(
                $"Unsupported delivery action outcome '{result.Outcome}'.")
        };
    }
}
