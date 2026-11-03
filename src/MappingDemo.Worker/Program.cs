using Confluent.Kafka;
using MappingDemo.Shared.Database;
using MappingDemo.Shared.Destinations;
using MappingDemo.Shared.Input;
using MappingDemo.Shared.Contracts;
using MappingDemo.Shared.Transformation;
using MappingDemo.Shared.Transformation.Adapters;
using MappingDemo.Worker;
using MappingDemo.Worker.Destinations;
using MappingDemo.Worker.Delivery;

var builder = Host.CreateApplicationBuilder(args);
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
builder.Services.AddMappingDatabase(builder.Configuration);
builder.Services.AddSingleton<ApiDestinationProfileObservability>();
builder.Services.AddSingleton<ITransformationLookupResolver,
    PostgreSqlTransformationLookupResolver>();
builder.Services.AddSingleton<TransformationModule>();
builder.Services.AddSingleton<ContractSchemaModule>();
builder.Services.AddSingleton<PostgreSqlVersionedContractRegistry>();
builder.Services.AddSingleton<IVersionedContractRegistrySource>(provider =>
    provider.GetRequiredService<PostgreSqlVersionedContractRegistry>());
builder.Services.AddSingleton<IVersionedContractRegistry>(provider =>
    new VersionedContractRegistryModule(
        provider.GetRequiredService<IVersionedContractRegistrySource>()));
builder.Services.AddSingleton<PayloadBindingModule>();
builder.Services.AddSingleton<IDeliverySecretProvider,
    EnvironmentDeliverySecretProvider>();
builder.Services.AddSingleton<DeliveryPayloadProtector>();
builder.Services.AddSingleton<JsonExportPayloadProtector>();
builder.Services.AddSingleton<JsonExportReadiness>();
builder.Services.AddSingleton<JsonExportControlReader>();
var jsonExportProfiles = builder.Configuration
    .GetSection("FileDestinationProfiles")
    .Get<JsonExportProfileOptions>() ?? new JsonExportProfileOptions();
builder.Services.AddSingleton(jsonExportProfiles.BuildCatalog());
builder.Services.AddSingleton<FileSystemJsonExportPublisher>();
builder.Services.AddSingleton<IJsonExportPublisher>(provider =>
    provider.GetRequiredService<FileSystemJsonExportPublisher>());
builder.Services.AddSingleton<IJsonExportArtifactCleaner>(provider =>
    provider.GetRequiredService<FileSystemJsonExportPublisher>());
builder.Services.AddSingleton<JsonFileFinalizer>();
builder.Services.AddSingleton<JsonExportRecoveryScheduler>();
builder.Services.AddSingleton<JsonExportCleanupService>();
builder.Services.AddSingleton<LegacyTableDestinationAdapter>();
builder.Services.AddSingleton<ApiContractDestinationAdapter>();
builder.Services.AddSingleton<JsonFileDestinationAdapter>();
builder.Services.AddSingleton<IDestinationAdapter>(provider =>
    provider.GetRequiredService<LegacyTableDestinationAdapter>());
builder.Services.AddSingleton<IDestinationAdapter>(provider =>
    provider.GetRequiredService<ApiContractDestinationAdapter>());
builder.Services.AddSingleton<IDestinationAdapter>(provider =>
    provider.GetRequiredService<JsonFileDestinationAdapter>());
builder.Services.AddSingleton<IDestinationRouter, DestinationRouter>();
var deliveryProfiles = builder.Configuration
    .GetSection("ApiDestinationProfiles")
    .Get<ApiDeliveryProfileOptions>() ?? new ApiDeliveryProfileOptions();
builder.Services.AddSingleton(deliveryProfiles.BuildCatalog());
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
builder.Services.AddSingleton<IAuthenticatedDeliveryTransport,
    AuthenticatedDeliveryTransport>();
builder.Services.AddSingleton<IDeliveryHttpAdapter, DeliveryHttpAdapter>();
builder.Services.AddSingleton<DeliveryJobHandler>();
builder.Services.AddSingleton<DeliveryControlReader>();

var inputRoot = builder.Configuration["Paths:InputRoot"];
if (string.IsNullOrWhiteSpace(inputRoot))
{
    throw new InvalidOperationException(
        "Configuration 'Paths:InputRoot' is not set.");
}

var archiveRoot = builder.Configuration["Paths:ArchiveRoot"];
if (string.IsNullOrWhiteSpace(archiveRoot))
{
    throw new InvalidOperationException(
        "Configuration 'Paths:ArchiveRoot' is not set.");
}

ArchivePathValidator.Validate(
    inputRoot,
    archiveRoot,
    builder.Environment.ContentRootPath);

var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
    ?? throw new InvalidOperationException(
        "Configuration 'Kafka:BootstrapServers' is not set.");

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = bootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true
    };

    return new ProducerBuilder<string, string>(config).Build();
});
builder.Services.AddSingleton(new ConsumerConfig
{
    BootstrapServers = bootstrapServers,
    GroupId = "mapping-file-import",
    EnableAutoCommit = false,
    AutoOffsetReset = AutoOffsetReset.Earliest
});
builder.Services.AddSingleton<InputRecordModule>();
builder.Services.AddSingleton<FileImportHandler>();
builder.Services.AddSingleton<RowNormalizeHandler>();
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<FileImportConsumer>();
builder.Services.AddHostedService<RowNormalizeConsumer>();
builder.Services.AddHostedService<DeliveryRetryScheduler>();
builder.Services.AddHostedService<DeliveryConsumer>();
builder.Services.AddHostedService<DeliveryRetentionService>();
builder.Services.AddHostedService<JsonExportConsumer>();
builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<JsonExportRecoveryScheduler>());
builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<JsonExportCleanupService>());

var host = builder.Build();
await host.Services.GetRequiredService<IApiDestinationProfileCatalog>()
    .GetSafeProfilesAsync();
host.Run();
