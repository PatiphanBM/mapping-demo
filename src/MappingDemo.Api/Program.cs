using MappingDemo.Api.Services;
using MappingDemo.Shared.Database;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var mappingConnectionString = builder.Configuration.GetConnectionString("Mapping")
    ?? throw new InvalidOperationException("Connection string 'Mapping' is not configured.");
var mappingConnectionStringForLog = new NpgsqlConnectionStringBuilder(mappingConnectionString)
{
    Password = "*****"
}.ConnectionString;

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddMappingDatabase(builder.Configuration);
builder.Services.AddHostedService<FileWatcherService>();

var app = builder.Build();

app.Logger.LogInformation(
    "Mapping database connection string: {ConnectionString}",
    mappingConnectionStringForLog);

var migrationRunner = app.Services.GetRequiredService<MigrationRunner>();
await migrationRunner.RunAsync();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
