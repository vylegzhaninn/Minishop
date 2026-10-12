using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniShop.Orders.Infrastructure.Options;

namespace MiniShop.Orders.Infrastructure.Messaging;

public sealed class KafkaOrderPublisher : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaOrderPublisher> _log;

    public KafkaOrderPublisher(IOptions<KafkaOptions> options, ILogger<KafkaOrderPublisher> log)
    {
        _topic = options.Value.Topic;
        _log = log;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All, 
            EnableIdempotence = true,
            MessageTimeoutMs = 10_000,
        }).Build();
    }

    public async Task PublishAsync<T>(string key, T payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, Json);
        await PublishRawAsync(key, json, ct);
    }

    public async Task PublishRawAsync(string key, string json, CancellationToken ct)
    {
        var result = await _producer.ProduceAsync(_topic,
            new Message<string, string> { Key = key, Value = json }, ct);
        _log.LogInformation("Published to {Topic} partition {Partition} offset {Offset}",
            _topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose() => _producer.Dispose();
}
