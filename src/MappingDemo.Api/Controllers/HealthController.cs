using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace MappingDemo.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public HealthController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "ok" });
    }

    [HttpGet("db")]
    public async Task<IActionResult> GetDatabase(CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            "SELECT 1",
            cancellationToken: cancellationToken);
        var result = await connection.ExecuteScalarAsync<int>(command);

        return Ok(new { status = "ok", database = result });
    }
}
