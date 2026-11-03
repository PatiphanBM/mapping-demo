using MappingDemo.Api.Contracts.DeliveryJobs;
using MappingDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("json-exports")]
public sealed class JsonExportsController(
    JsonExportOperationsService exports,
    IDeliveryOperationsAuthorizer authorizer) : ControllerBase
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

        return Ok(await exports.GetForFileJobAsync(
            fileJobId,
            cancellationToken));
    }

    [HttpPost("{id:long}/retry")]
    public async Task<IActionResult> Retry(
        long id,
        RetryJsonExportRequest request,
        CancellationToken cancellationToken)
    {
        var operatorId = Request.Headers["X-Operator-Id"].FirstOrDefault();
        if (!authorizer.IsAuthorized(User, operatorId))
        {
            return Forbid();
        }
        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Length > 500)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "A retry reason between 1 and 500 characters is required."
            });
        }

        var result = await exports.RetryAsync(
            id,
            operatorId!,
            request.Reason.Trim(),
            cancellationToken);
        return result.Outcome switch
        {
            DeliveryActionOutcome.Accepted => Accepted(new
            {
                JsonExportJobId = result.DeliveryJobId
            }),
            DeliveryActionOutcome.NotFound => NotFound(),
            DeliveryActionOutcome.Conflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Only a RetryableFailed JSON export can be retried."
            }),
            _ => throw new InvalidOperationException(
                $"Unsupported JSON export action outcome '{result.Outcome}'.")
        };
    }
}
