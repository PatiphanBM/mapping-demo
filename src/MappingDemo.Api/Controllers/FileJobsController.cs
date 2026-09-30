using MappingDemo.Api.Contracts.FileJobs;
using MappingDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("file-jobs")]
public sealed class FileJobsController : ControllerBase
{
    private readonly FileJobService _fileJobService;

    public FileJobsController(FileJobService fileJobService)
    {
        _fileJobService = fileJobService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var fileJobs = await _fileJobService.GetAllAsync(cancellationToken);
        return Ok(fileJobs);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var fileJob = await _fileJobService.GetByIdAsync(
            id,
            cancellationToken);

        return fileJob is null ? NotFound() : Ok(fileJob);
    }

    [HttpGet("{id:long}/errors")]
    public async Task<IActionResult> GetErrors(
        long id,
        CancellationToken cancellationToken)
    {
        var fileJob = await _fileJobService.GetByIdAsync(
            id,
            cancellationToken);

        if (fileJob is null)
        {
            return NotFound();
        }

        var errors = await _fileJobService.GetErrorsAsync(
            id,
            cancellationToken);
        return Ok(errors);
    }

    [HttpGet("{id:long}/rows/{rowNumber:int:min(1)}/history")]
    public async Task<IActionResult> GetRowHistory(
        long id,
        int rowNumber,
        CancellationToken cancellationToken)
    {
        var fileJob = await _fileJobService.GetByIdAsync(
            id,
            cancellationToken);

        if (fileJob is null)
        {
            return NotFound();
        }

        var history = await _fileJobService.GetRowHistoryAsync(
            id,
            rowNumber,
            cancellationToken);
        return Ok(history);
    }

    [HttpPost("{id:long}/retry")]
    public async Task<IActionResult> Retry(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _fileJobService.RetryAsync(
            id,
            cancellationToken);

        return result switch
        {
            JobRetryResult.Retried => Accepted(),
            JobRetryResult.NotFound => NotFound(),
            JobRetryResult.Conflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "File job cannot be retried.",
                Detail = "Only an ImportFailed file job can be retried."
            }),
            _ => throw new InvalidOperationException(
                $"Unsupported retry result '{result}'.")
        };
    }

    [HttpPost("{id:long}/reprocess")]
    public async Task<IActionResult> Reprocess(
        long id,
        ReprocessFileJobRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _fileJobService.ReprocessAsync(
            id,
            request.VersionId,
            cancellationToken);

        return result.Outcome switch
        {
            FileReprocessOutcome.Accepted => Accepted(
                new ReprocessFileJobResponse(result.RowJobsCreated)),
            FileReprocessOutcome.FileJobNotFound => NotFound(),
            FileReprocessOutcome.InvalidVersion => BadRequest(
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid config version for reprocess.",
                    Detail = result.Error
                }),
            FileReprocessOutcome.Conflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "File job cannot be reprocessed.",
                Detail = "A Source row already has a Pending normalization job."
            }),
            _ => throw new InvalidOperationException(
                $"Unsupported reprocess outcome '{result.Outcome}'.")
        };
    }
}
