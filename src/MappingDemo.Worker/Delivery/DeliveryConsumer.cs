using System.Text.Json;
using Confluent.Kafka;
using MappingDemo.Shared.Messaging;

namespace MappingDemo.Worker.Delivery;

public sealed class DeliveryConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly ConsumerConfig _config;
    private readonly int _consumerCount;
    private readonly DeliveryJobHandler _handler;
    private readonly ILogger<DeliveryConsumer> _logger;

    public DeliveryConsumer(
        ConsumerConfig baseConfig,
        IConfiguration configuration,
        DeliveryJobHandler handler,
        ILogger<DeliveryConsumer> logger)
    {
        _config = new ConsumerConfig
        {
            BootstrapServers = baseConfig.BootstrapServers,
            GroupId = "mapping-api-delivery",
            EnableAutoCommit = false,
            AutoOffsetReset = baseConfig.AutoOffsetReset
        };
        _consumerCount = configuration.GetValue(
            "Kafka:DeliveryConsumers",
            1);
        _handler = handler;
        _logger = logger;

        if (_consumerCount <= 0)
        {
            throw new InvalidOperationException(
                "Configuration 'Kafka:DeliveryConsumers' must be greater than zero.");
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
        using var consumer = new ConsumerBuilder<string, string>(_config)
            .Build();
        consumer.Subscribe(Topics.ApiDelivery);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                var request = JsonSerializer.Deserialize<DeliveryRequested>(
                        result.Message.Value,
                        JsonOptions)
                    ?? throw new InvalidOperationException(
                        "Delivery message payload is empty.");
                _handler.HandleAsync(request.DeliveryJobId, stoppingToken)
                    .GetAwaiter()
                    .GetResult();
                consumer.Commit(result);
                _logger.LogDebug(
                    "Delivery consumer {ConsumerNumber} committed job {DeliveryJobId}",
                    consumerNumber,
                    request.DeliveryJobId);
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
