using Confluent.Kafka;
using MappingDemo.Shared.Messaging;

namespace MappingDemo.Worker;

public sealed class FileImportConsumer : BackgroundService
{
    private readonly ConsumerConfig _config;
    private readonly int _consumerCount;
    private readonly FileImportHandler _handler;
    private readonly ILogger<FileImportConsumer> _logger;

    public FileImportConsumer(
        ConsumerConfig config,
        IConfiguration configuration,
        FileImportHandler handler,
        ILogger<FileImportConsumer> logger)
    {
        _config = config;
        _consumerCount = configuration.GetValue<int>(
            "Kafka:FileImportConsumers");
        _handler = handler;
        _logger = logger;

        if (_consumerCount <= 0)
        {
            throw new InvalidOperationException(
                "Configuration 'Kafka:FileImportConsumers' must be greater than zero.");
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
                    "File import consumer {ConsumerNumber} assigned partitions {Partitions}",
                    consumerNumber,
                    assignedPartitions);
            })
            .Build();
        consumer.Subscribe(Topics.FileImport);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);

                _logger.LogInformation(
                    "Consumer {ConsumerNumber} consumed {Topic}, partition {Partition}, offset {Offset}, key {Key}",
                    consumerNumber,
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value,
                    result.Message.Key);

                var shouldCommit = _handler
                    .HandleAsync(result.Message.Value, stoppingToken)
                    .GetAwaiter()
                    .GetResult();

                if (shouldCommit)
                {
                    consumer.Commit(result);
                }
                else
                {
                    consumer.Pause([result.TopicPartition]);
                    _logger.LogInformation(
                        "Paused partition {Partition} until file job {FileJobId} processing is implemented",
                        result.Partition.Value,
                        result.Message.Key);
                }
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
