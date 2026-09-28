using Confluent.Kafka;
using MappingDemo.Shared.Database;
using MappingDemo.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddMappingDatabase(builder.Configuration);

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
builder.Services.AddHostedService<OutboxDispatcher>();

var host = builder.Build();
host.Run();
