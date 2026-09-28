using Confluent.Kafka;
using Dapper;
using Npgsql;

namespace MappingDemo.Worker;

public sealed class OutboxDispatcher : BackgroundService
{
    private const string SelectPendingSql = """
        SELECT
            id,
            topic,
            message_key AS MessageKey,
            payload::text AS Payload
        FROM outbox
        WHERE sent_at IS NULL
        ORDER BY id
        LIMIT 100;
        """;

    private const string MarkSentSql = """
        UPDATE outbox
        SET sent_at = now()
        WHERE id = @Id;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        NpgsqlDataSource dataSource,
        IProducer<string, string> producer,
        ILogger<OutboxDispatcher> logger)
    {
        _dataSource = dataSource;
        _producer = producer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var messages = await LoadPendingAsync(stoppingToken);

            if (messages.Count > 0)
            {
                _logger.LogDebug(
                    "Found {OutboxCount} pending outbox messages",
                    messages.Count);
            }

            foreach (var message in messages)
            {
                try
                {
                    await DispatchAsync(message, stoppingToken);
                }
                catch (ProduceException<string, string> exception)
                {
                    _logger.LogError(
                        exception,
                        "Failed to dispatch outbox message {OutboxId} to topic {Topic}",
                        message.Id,
                        message.Topic);
                    break;
                }
            }
        }
    }

    private async Task<IReadOnlyList<OutboxMessage>> LoadPendingAsync(
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            SelectPendingSql,
            cancellationToken: cancellationToken);
        var messages = await connection.QueryAsync<OutboxMessage>(command);

        return messages.AsList();
    }

    private async Task DispatchAsync(
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        var deliveryResult = await _producer.ProduceAsync(
            outboxMessage.Topic,
            new Message<string, string>
            {
                Key = outboxMessage.MessageKey,
                Value = outboxMessage.Payload
            },
            cancellationToken);

        await MarkSentAsync(outboxMessage.Id, cancellationToken);

        _logger.LogInformation(
            "Dispatched outbox message {OutboxId} to {Topic}, partition {Partition}, offset {Offset}",
            outboxMessage.Id,
            deliveryResult.Topic,
            deliveryResult.Partition.Value,
            deliveryResult.Offset.Value);
    }

    private async Task MarkSentAsync(
        long outboxId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            MarkSentSql,
            new { Id = outboxId },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private sealed record OutboxMessage(
        long Id,
        string Topic,
        string MessageKey,
        string Payload);
}
