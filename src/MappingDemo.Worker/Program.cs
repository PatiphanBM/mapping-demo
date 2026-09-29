using Confluent.Kafka;
using MappingDemo.Shared.Csv;
using MappingDemo.Shared.Database;
using MappingDemo.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddMappingDatabase(builder.Configuration);

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
builder.Services.AddSingleton<CsvRecordReader>();
builder.Services.AddSingleton<FileImportHandler>();
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<FileImportConsumer>();

var host = builder.Build();
host.Run();
