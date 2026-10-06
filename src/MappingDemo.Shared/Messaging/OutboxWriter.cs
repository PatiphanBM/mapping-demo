using System.Text.Json;
using Dapper;
using Npgsql;

namespace MappingDemo.Shared.Messaging;

public static class OutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string InsertOutboxSql = """
        INSERT INTO outbox (
            topic,
            message_key,
            payload)
        VALUES (
            @Topic,
            @MessageKey,
            CAST(@PayloadJson AS jsonb));
        """;

    public static async Task AddAsync<TMessage>(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string topic,
        string key,
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(message);

        var parameters = new
        {
            Topic = topic,
            MessageKey = key,
            PayloadJson = JsonSerializer.Serialize(message, JsonOptions)
        };
        //Insert Outbox
        var command = new CommandDefinition(
            InsertOutboxSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }
}
