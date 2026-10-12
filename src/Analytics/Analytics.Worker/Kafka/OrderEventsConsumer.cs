using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using MiniShop.Analytics.Worker.ClickHouse;

namespace MiniShop.Analytics.Worker.Kafka;

public sealed class OrderEventsConsumer(
    IOptions<KafkaSettings> kafka,
    OrderEventsSink sink,
    ILogger<OrderEventsConsumer> log) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private int _poisonSkipped;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await sink.EnsureSchemaAsync(ct);

        var config = new ConsumerConfig
        {
            BootstrapServers = kafka.Value.BootstrapServers,
            GroupId = kafka.Value.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false, 
            EnableAutoOffsetStore = false, 
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetPartitionsRevokedHandler((c, _) =>
            {

                log.LogWarning("Partitions revoked, dropping buffered batch to avoid duplicates lag");
                c.Commit(); 
            })
            .Build();
        consumer.Subscribe(kafka.Value.Topic);
        log.LogInformation("Subscribed to {Topic}", kafka.Value.Topic);

        var batch = new List<(OrderEvent Event, TopicPartitionOffset Tpo)>(cap: 200);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var r = consumer.Consume(TimeSpan.FromSeconds(1));
                if (r is null)
                {
                    await FlushAsync(consumer, batch, ct);
                    continue;
                }

                OrderEvent? evt;
                try
                {
                    evt = JsonSerializer.Deserialize<OrderEvent>(r.Message.Value, Json);
                }
                catch (JsonException ex)
                {
                    _poisonSkipped++;
                    log.LogWarning(ex, "Skipping poison message at {Tpo} (total skipped: {Skipped})",
                        r.TopicPartitionOffset, _poisonSkipped);
                    consumer.StoreOffset(r);
                    consumer.Commit();
                    continue;
                }

                if (evt is null)
                {
                    consumer.StoreOffset(r); 
                    continue;
                }

                batch.Add((evt, r.TopicPartitionOffset));

                if (batch.Count >= 200)
                    await FlushAsync(consumer, batch, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex)
            {
                log.LogWarning(ex, "Kafka consume error, will retry");
            }
        }

        try { await FlushAsync(consumer, batch, CancellationToken.None); }
        catch (Exception ex) { log.LogError(ex, "Flush on shutdown failed"); }
    }

    private async Task FlushAsync(
        IConsumer<string, string> consumer,
        List<(OrderEvent Event, TopicPartitionOffset Tpo)> batch,
        CancellationToken ct)
    {
        if (batch.Count == 0) return;
        await sink.InsertBatchAsync(batch.Select(x => x.Event).ToList(), ct);
        consumer.StoreOffset(batch[^1].Tpo); 
        consumer.Commit();
        batch.Clear();
    }
}

public sealed record OrderEvent(
    Guid EventId, string EventType, Guid OrderId, Guid CustomerId,
    long TotalMinor, string Currency, DateTimeOffset OccurredAt);

public sealed class KafkaSettings
{
    public string BootstrapServers { get; init; } = "kafka:9092";
    public string Topic { get; init; } = "orders.events";
    public string GroupId { get; init; } = "analytics-worker";
}
