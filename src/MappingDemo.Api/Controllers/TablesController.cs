using MappingDemo.Api.Contracts.Tables;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Tables;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("tables")]
public class TablesController : ControllerBase
{
    private readonly TableService _tableService;

    public TablesController(TableService tableService)
    {
        _tableService = tableService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var tables = await _tableService.GetAllAsync(cancellationToken);

        return Ok(tables);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var table = await _tableService.GetByIdAsync(id, cancellationToken);

        return table is null ? NotFound() : Ok(table);
    }

    [HttpPost("{id:long}/columns")]
    public async Task<IActionResult> AddColumn(
        long id,
        ColumnRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableService.GetByIdAsync(id, cancellationToken);

        if (table is null)
        {
            return NotFound();
        }

        var column = new ColumnDefinition(
            request.Name,
            request.DataType,
            request.IsRequired,
            Ordinal: 1);
        var definition = new TableDefinition(
            table.Name,
            table.Kind,
            [column]);
        var errors = TableDefinitionValidator.Validate(definition);

        foreach (var error in errors)
        {
            const string fieldPrefix = "columns[0].";
            var field = error.Field.StartsWith(
                fieldPrefix,
                StringComparison.Ordinal)
                ? error.Field[fieldPrefix.Length..]
                : error.Field;
            ModelState.AddModelError(field, error.Message);
        }

        if (table.Columns.Any(existing => existing.Name == request.Name))
        {
            ModelState.AddModelError("name", "Column name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var added = await _tableService.AddColumnAsync(
                id,
                request,
                cancellationToken);

            return added ? NoContent() : NotFound();
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Column name already exists.",
                Detail = $"A column named '{request.Name}' already exists."
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateTableRequest request,
        CancellationToken cancellationToken)
    {
        var columns = request.Columns
            .Select((column, index) => new ColumnDefinition(
                column.Name,
                column.DataType,
                column.IsRequired,
                index + 1))
            .ToArray();
        var definition = new TableDefinition(
            request.Name,
            request.Kind,
            columns);

        var errors = TableDefinitionValidator.Validate(definition);

        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        long tableId;

        try
        {
            tableId = await _tableService.CreateAsync(
                definition,
                cancellationToken);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Table name already exists.",
                Detail = $"A table named '{request.Name}' already exists."
            });
        }

        var response = new TableResponse(
            tableId,
            request.Name,
            request.Kind,
            request.Columns);

        return CreatedAtAction(
            nameof(GetById),
            new { id = tableId },
            response);
    }
}
