using System.Text.Json;
using Confluent.Kafka;
using MappingDemo.Shared.Messaging;

namespace MappingDemo.Worker.DatabaseDelivery;

public sealed class DatabaseDeliveryConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly ConsumerConfig _config;
    private readonly int _consumerCount;
    private readonly DatabaseDeliveryJobHandler _handler;
    private readonly ILogger<DatabaseDeliveryConsumer> _logger;

    public DatabaseDeliveryConsumer(
        ConsumerConfig baseConfig,
        IConfiguration configuration,
        DatabaseDeliveryJobHandler handler,
        ILogger<DatabaseDeliveryConsumer> logger)
    {
        _config = new ConsumerConfig
        {
            BootstrapServers = baseConfig.BootstrapServers,
            GroupId = "mapping-database-delivery",
            EnableAutoCommit = false,
            AutoOffsetReset = baseConfig.AutoOffsetReset
        };
        _consumerCount = configuration.GetValue(
            "Kafka:DatabaseDeliveryConsumers",
            1);
        _handler = handler;
        _logger = logger;
        if (_consumerCount <= 0)
        {
            throw new InvalidOperationException(
                "Configuration 'Kafka:DatabaseDeliveryConsumers' must be greater than zero.");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = Enumerable.Range(1, _consumerCount)
            .Select(number => Task.Factory.StartNew(
                () => Consume(number, stoppingToken),
                stoppingToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();
        return Task.WhenAll(tasks);
    }

    private void Consume(int consumerNumber, CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(_config).Build();
        consumer.Subscribe(Topics.DatabaseDelivery);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                var request = JsonSerializer.Deserialize<DatabaseDeliveryRequested>(
                        result.Message.Value,
                        JsonOptions)
                    ?? throw new InvalidOperationException(
                        "Database delivery message payload is empty.");
                _handler.HandleAsync(
                        request.DatabaseDeliveryJobId,
                        stoppingToken)
                    .GetAwaiter()
                    .GetResult();
                consumer.Commit(result);
                _logger.LogDebug(
                    "Database delivery consumer {ConsumerNumber} committed job {DatabaseDeliveryJobId}",
                    consumerNumber,
                    request.DatabaseDeliveryJobId);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }
}
