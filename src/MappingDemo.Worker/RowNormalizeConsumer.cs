using Confluent.Kafka;
using MappingDemo.Shared.Messaging;

namespace MappingDemo.Worker;

public sealed class RowNormalizeConsumer : BackgroundService
{
    private const string ConsumerGroup = "mapping-row-normalize";

    private readonly ConsumerConfig _config;
    private readonly int _consumerCount;
    private readonly RowNormalizeHandler _handler;
    private readonly ILogger<RowNormalizeConsumer> _logger;

    public RowNormalizeConsumer(
        ConsumerConfig fileImportConfig,
        IConfiguration configuration,
        RowNormalizeHandler handler,
        ILogger<RowNormalizeConsumer> logger)
    {
        _config = new ConsumerConfig
        {
            BootstrapServers = fileImportConfig.BootstrapServers,
            GroupId = ConsumerGroup,
            EnableAutoCommit = false,
            AutoOffsetReset = fileImportConfig.AutoOffsetReset
        };
        _consumerCount = configuration.GetValue<int>(
            "Kafka:NormalizeConsumers");
        _handler = handler;
        _logger = logger;

        if (_consumerCount <= 0)
        {
            throw new InvalidOperationException(
                "Configuration 'Kafka:NormalizeConsumers' must be greater than zero.");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerTasks = Enumerable
            .Range(1, _consumerCount)
            .Select(consumerNumber => Task.Factory.StartNew(
                () => Consume(consumerNumber, stoppingToken),
                stoppingToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        return Task.WhenAll(consumerTasks);
    }

    private void Consume(
        int consumerNumber,
        CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(_config)
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                var assignedPartitions = string.Join(
                    ", ",
                    partitions.Select(partition => partition.Partition.Value));

                _logger.LogInformation(
                    "Row normalize consumer {ConsumerNumber} in group {ConsumerGroup} assigned partitions {Partitions}",
                    consumerNumber,
                    ConsumerGroup,
                    assignedPartitions);
            })
            .Build();
        consumer.Subscribe(Topics.RowNormalize);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);

                _logger.LogInformation(
                    "Row normalize consumer {ConsumerNumber} consumed {Topic}, partition {Partition}, offset {Offset}, key {Key}",
                    consumerNumber,
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value,
                    result.Message.Key);

                _handler
                    .HandleAsync(result.Message.Value, stoppingToken)
                    .GetAwaiter()
                    .GetResult();
                consumer.Commit(result);
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
