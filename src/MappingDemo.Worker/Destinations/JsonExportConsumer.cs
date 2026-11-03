using System.Text.Json;
using Confluent.Kafka;
using MappingDemo.Shared.Messaging;

namespace MappingDemo.Worker.Destinations;

public sealed class JsonExportConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly ConsumerConfig _config;
    private readonly int _consumerCount;
    private readonly JsonFileFinalizer _finalizer;
    private readonly ILogger<JsonExportConsumer> _logger;

    public JsonExportConsumer(
        ConsumerConfig baseConfig,
        IConfiguration configuration,
        JsonFileFinalizer finalizer,
        ILogger<JsonExportConsumer> logger)
    {
        _config = new ConsumerConfig
        {
            BootstrapServers = baseConfig.BootstrapServers,
            GroupId = "mapping-json-export",
            EnableAutoCommit = false,
            AutoOffsetReset = baseConfig.AutoOffsetReset
        };
        _consumerCount = configuration.GetValue(
            "Kafka:JsonExportConsumers",
            1);
        _finalizer = finalizer;
        _logger = logger;

        if (_consumerCount <= 0)
        {
            throw new InvalidOperationException(
                "Configuration 'Kafka:JsonExportConsumers' must be greater than zero.");
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
        consumer.Subscribe(Topics.JsonExportFinalize);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                var request = JsonSerializer.Deserialize<JsonExportRequested>(
                        result.Message.Value,
                        JsonOptions)
                    ?? throw new InvalidOperationException(
                        "JSON export message payload is empty.");
                _finalizer.HandleAsync(
                        request.JsonExportJobId,
                        stoppingToken)
                    .GetAwaiter()
                    .GetResult();
                consumer.Commit(result);
                _logger.LogDebug(
                    "JSON export consumer {ConsumerNumber} committed job {JsonExportJobId}",
                    consumerNumber,
                    request.JsonExportJobId);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }
}
