using MappingDemo.Api.Contracts.DatabaseDestinations;
using MappingDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("database-delivery-operations")]
public sealed class DatabaseDeliveryOperationsController(
    DatabaseDeliveryOperationsService operations,
    IDeliveryOperationsAuthorizer authorizer) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await operations.GetAsync(cancellationToken));

    [HttpPost("{jobId:long}/retry")]
    public Task<IActionResult> Retry(
        long jobId,
        DatabaseDeliveryResolutionRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(
            jobId,
            request,
            (operatorId, reason, correlationId) => operations.RetryAsync(
                jobId,
                operatorId,
                reason,
                correlationId,
                request.AcceptDuplicateRisk,
                cancellationToken));

    [HttpPost("{jobId:long}/mark-delivered")]
    public Task<IActionResult> MarkDelivered(
        long jobId,
        DatabaseDeliveryResolutionRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(
            jobId,
            request,
            (operatorId, reason, correlationId) => operations.MarkDeliveredAsync(
                jobId,
                operatorId,
                reason,
                correlationId,
                cancellationToken));

    [HttpPost("{jobId:long}/mark-failed")]
    public Task<IActionResult> MarkFailed(
        long jobId,
        DatabaseDeliveryResolutionRequest request,
        CancellationToken cancellationToken) => ExecuteAsync(
            jobId,
            request,
            (operatorId, reason, correlationId) => operations.MarkFailedAsync(
                jobId,
                operatorId,
                reason,
                correlationId,
                cancellationToken));

    private async Task<IActionResult> ExecuteAsync(
        long jobId,
        DatabaseDeliveryResolutionRequest request,
        Func<string, string, string,
            Task<DatabaseDeliveryResolutionResult>> action)
    {
        var operatorId = Request.Headers["X-Operator-Id"].FirstOrDefault();
        if (!authorizer.IsAuthorized(User, operatorId))
        {
            return Forbid();
        }
        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Length > 500 ||
            string.IsNullOrWhiteSpace(request.CorrelationId) ||
            request.CorrelationId.Length > 200)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Reason and correlation ID are required."
            });
        }

        var result = await action(
            operatorId!.Trim(),
            request.Reason.Trim(),
            request.CorrelationId.Trim());
        return result switch
        {
            DatabaseDeliveryResolutionResult.Updated => NoContent(),
            DatabaseDeliveryResolutionResult.NotFound => NotFound(),
            DatabaseDeliveryResolutionResult.DuplicateRiskAcceptanceRequired =>
                Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Retry requires explicit duplicate-risk acceptance."
                }),
            _ => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Database delivery job cannot be resolved from its current state."
            })
        };
    }
}
