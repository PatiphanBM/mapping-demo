using MappingDemo.Api;
using MappingDemo.Api.Options;
using MappingDemo.Api.Services;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;
using MappingDemo.Shared.Transformation.Adapters;
using Microsoft.Extensions.Options;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var apiProfileCatalogSourceMode =
    ApiDestinationProfileCatalogSourceModeParser.Parse(
        builder.Configuration[
            "ApiDestinationProfileCatalog:SourceMode"]);
var apiProfileCompareAuthority = apiProfileCatalogSourceMode ==
    ApiDestinationProfileCatalogSourceMode.Compare
        ? ApiDestinationProfileCatalogSourceModeParser.Parse(
            builder.Configuration[
                "ApiDestinationProfileCatalog:CompareAuthoritativeSource"]
            ?? throw new InvalidOperationException(
                "CompareAuthoritativeSource is required in Compare mode."))
        : ApiDestinationProfileCatalogSourceMode.Postgres;
if (apiProfileCompareAuthority == ApiDestinationProfileCatalogSourceMode.Compare)
{
    throw new InvalidOperationException(
        "CompareAuthoritativeSource must be Configuration or Postgres.");
}
var contractRegistrySourceMode = ContractRegistrySourceModeParser.Parse(
    builder.Configuration["ContractRegistry:SourceMode"]);

var mappingConnectionString = builder.Configuration.GetConnectionString("Mapping")
    ?? throw new InvalidOperationException("Connection string 'Mapping' is not configured.");
var mappingConnectionStringForLog = new NpgsqlConnectionStringBuilder(mappingConnectionString)
{
    Password = "*****"
}.ConnectionString;

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(ApiJsonOptions.Configure);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(OpenApiConfiguration.Configure);
builder.Services
    .AddOptions<PathOptions>()
    .Bind(builder.Configuration.GetSection(PathOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.InputRoot),
        "Paths:InputRoot is required.")
    .ValidateOnStart();
builder.Services
    .AddOptions<WatcherOptions>()
    .Bind(builder.Configuration.GetSection(WatcherOptions.SectionName))
    .Validate(
        options => options.ConfigRefreshSeconds > 0,
        "Watcher:ConfigRefreshSeconds must be greater than zero.")
    .Validate(
        options => options.ScanIntervalSeconds > 0,
        "Watcher:ScanIntervalSeconds must be greater than zero.")
    .ValidateOnStart();
builder.Services
    .AddOptions<ApiDestinationProfilesOptions>()
    .Bind(builder.Configuration.GetSection(ApiDestinationProfilesOptions.SectionName))
    .Validate(options =>
    {
        try
        {
            options.BuildCatalog();
            return true;
        }
        catch (ApiDestinationProfileValidationException)
        {
            return false;
        }
    }, "One or more API destination profiles are invalid.")
    .ValidateOnStart();
builder.Services
    .AddOptions<FileDestinationProfilesOptions>()
    .Bind(builder.Configuration.GetSection(FileDestinationProfilesOptions.SectionName))
    .Validate(options =>
    {
        try
        {
            options.BuildCatalog();
            return true;
        }
        catch (FileDestinationProfileValidationException)
        {
            return false;
        }
    }, "One or more File destination profiles are invalid.")
    .ValidateOnStart();
builder.Services.AddMappingDatabase(builder.Configuration);
builder.Services.AddSingleton<ApiDestinationProfileObservability>();
builder.Services.AddSingleton<ApiDestinationProfileCatalog>(serviceProvider =>
    serviceProvider.GetRequiredService<IOptions<ApiDestinationProfilesOptions>>()
        .Value.BuildCatalog());
builder.Services.AddSingleton<PostgreSqlApiDestinationProfileCatalog>();
builder.Services.AddSingleton(serviceProvider =>
    new ApiDestinationProfileCatalogModule(
        apiProfileCatalogSourceMode switch
        {
            ApiDestinationProfileCatalogSourceMode.Configuration =>
                serviceProvider.GetRequiredService<
                    ApiDestinationProfileCatalog>(),
            ApiDestinationProfileCatalogSourceMode.Compare =>
                new ComparingApiDestinationProfileCatalog(
                    serviceProvider.GetRequiredService<
                        ApiDestinationProfileCatalog>(),
                    serviceProvider.GetRequiredService<
                        PostgreSqlApiDestinationProfileCatalog>(),
                    apiProfileCompareAuthority),
            ApiDestinationProfileCatalogSourceMode.Postgres =>
                serviceProvider.GetRequiredService<
                    PostgreSqlApiDestinationProfileCatalog>(),
            _ => throw new InvalidOperationException(
                "Unsupported API Destination Profile catalog source mode.")
        },
        ApiDestinationProfileCachePolicy.Default,
        TimeProvider.System,
        serviceProvider.GetRequiredService<
            ApiDestinationProfileObservability>()));
builder.Services.AddSingleton<IApiDestinationProfileCatalog>(serviceProvider =>
    serviceProvider.GetRequiredService<ApiDestinationProfileCatalogModule>());
builder.Services.AddSingleton<IApiDestinationProfileCatalogCacheInvalidator>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            ApiDestinationProfileCatalogModule>());
builder.Services.AddSingleton(serviceProvider =>
    serviceProvider.GetRequiredService<IOptions<FileDestinationProfilesOptions>>()
        .Value.BuildCatalog());
builder.Services.AddSingleton<ContractSchemaModule>();
builder.Services.AddSingleton<ContractRegistryObservability>();
builder.Services.AddSingleton<PostgreSqlVersionedContractRegistry>();
builder.Services.AddSingleton(serviceProvider =>
    BundledVersionedContractRegistryFactory.Create(
        serviceProvider.GetRequiredService<IHostEnvironment>()));
builder.Services.AddSingleton<IVersionedContractRegistrySource>(serviceProvider =>
    contractRegistrySourceMode switch
    {
        ContractRegistrySourceMode.Bundled =>
            serviceProvider.GetRequiredService<
                BundledVersionedContractRegistry>(),
        ContractRegistrySourceMode.Postgres =>
            serviceProvider.GetRequiredService<
                PostgreSqlVersionedContractRegistry>(),
        _ => throw new InvalidOperationException(
            "Unsupported Contract Registry source mode.")
    });
builder.Services.AddSingleton<IVersionedContractRegistry>(serviceProvider =>
    new VersionedContractRegistryModule(
        serviceProvider.GetRequiredService<
            IVersionedContractRegistrySource>(),
        observability: serviceProvider.GetRequiredService<
            ContractRegistryObservability>()));
builder.Services.AddSingleton<IContractSchemaFetcher,
    RegistryContractSchemaFetcher>();
builder.Services.AddSingleton<ContractRegistryBootstrapper>();
builder.Services.AddSingleton<ContractRegistryModule>();
builder.Services.AddSingleton<PayloadBindingModule>();
builder.Services.AddSingleton<ITransformationLookupResolver,
    PostgreSqlTransformationLookupResolver>();
builder.Services.AddSingleton<TransformationModule>();
builder.Services.AddSingleton<InputRecordModule>();
builder.Services.AddSingleton<TableAdministrationObservability>();
builder.Services.AddSingleton<ITableSchemaInspector,
    PostgreSqlTableSchemaInspector>();
builder.Services.AddSingleton<TableVerificationRateLimiter>();
builder.Services.AddSingleton<MappingDraftService>();
builder.Services.AddSingleton<JsonFilePreviewModule>();
builder.Services.AddSingleton<ActiveConfigProvider>();
builder.Services.AddSingleton<WatcherRegistry>();
builder.Services.AddSingleton<FileIntake>();
builder.Services.AddScoped<MappingConfigService>();
builder.Services.AddScoped<TableService>();
builder.Services.AddScoped<FileJobService>();
builder.Services.AddScoped<RowJobService>();
builder.Services.AddScoped<DeliveryJobService>();
builder.Services.AddScoped<JsonExportOperationsService>();
builder.Services.AddScoped<DeliveryOperationsService>();
builder.Services.AddScoped<ApiDestinationProfileAdministrationService>();
builder.Services.AddSingleton<IProfileSecretReferenceValidator,
    EnvironmentProfileSecretReferenceValidator>();
builder.Services.AddSingleton<IProfileAdministrationAuthorizer,
    ProfileAdministrationAuthorizer>();
builder.Services.AddSingleton<IContractRegistryAuthorizer,
    ContractRegistryAuthorizer>();
builder.Services.AddSingleton<ITableAdministrationAuthorizer,
    TableAdministrationAuthorizer>();
builder.Services.AddSingleton<IDeliveryResendAuthorizer,
    DeliveryResendAuthorizer>();
builder.Services.AddSingleton<IDeliveryOperationsAuthorizer,
    DeliveryOperationsAuthorizer>();
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
if (contractRegistrySourceMode == ContractRegistrySourceMode.Postgres)
{
    await app.Services.GetRequiredService<ContractRegistryBootstrapper>()
        .RunAsync();
}
await app.Services.GetRequiredService<IApiDestinationProfileCatalog>()
    .GetSafeProfilesAsync();

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
