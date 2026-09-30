using MappingDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("row-jobs")]
public sealed class RowJobsController : ControllerBase
{
    private readonly RowJobService _rowJobService;

    public RowJobsController(RowJobService rowJobService)
    {
        _rowJobService = rowJobService;
    }

    [HttpPost("{id:long}/retry")]
    public async Task<IActionResult> Retry(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _rowJobService.RetryAsync(
            id,
            cancellationToken);

        return result switch
        {
            JobRetryResult.Retried => Accepted(),
            JobRetryResult.NotFound => NotFound(),
            JobRetryResult.Conflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Row job cannot be retried.",
                Detail = "Only a Failed row job can be retried."
            }),
            _ => throw new InvalidOperationException(
                $"Unsupported retry result '{result}'.")
        };
    }
}
