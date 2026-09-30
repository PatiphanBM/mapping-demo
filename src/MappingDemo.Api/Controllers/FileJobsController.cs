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
}
