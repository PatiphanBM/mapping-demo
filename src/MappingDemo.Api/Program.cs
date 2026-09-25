using MappingDemo.Api.Options;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Database;
using Microsoft.Extensions.Options;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var mappingConnectionString = builder.Configuration.GetConnectionString("Mapping")
    ?? throw new InvalidOperationException("Connection string 'Mapping' is not configured.");
var mappingConnectionStringForLog = new NpgsqlConnectionStringBuilder(mappingConnectionString)
{
    Password = "*****"
}.ConnectionString;

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddOptions<PathOptions>()
    .Bind(builder.Configuration.GetSection(PathOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.InputRoot),
        "Paths:InputRoot is required.")
    .ValidateOnStart();
builder.Services.AddMappingDatabase(builder.Configuration);
builder.Services.AddScoped<MappingConfigService>();
builder.Services.AddScoped<TableService>();
builder.Services.AddHostedService<FileWatcherService>();

var app = builder.Build();

app.Logger.LogInformation(
    "Mapping database connection string: {ConnectionString}",
    mappingConnectionStringForLog);
var pathOptions = app.Services.GetRequiredService<IOptions<PathOptions>>().Value;
var inputRootPath = Path.GetFullPath(
    pathOptions.InputRoot,
    app.Environment.ContentRootPath);
app.Logger.LogInformation("Input root path: {InputRoot}", inputRootPath);

var migrationRunner = app.Services.GetRequiredService<MigrationRunner>();
await migrationRunner.RunAsync();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "openapi/{documentName}.json";
    });
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();
