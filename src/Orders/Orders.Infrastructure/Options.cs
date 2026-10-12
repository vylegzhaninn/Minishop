using System.ComponentModel.DataAnnotations;

namespace MiniShop.Orders.Infrastructure.Options;

public sealed class KafkaOptions
{
    public const string Section = "Kafka";
    [Required] public string BootstrapServers { get; init; } = "kafka:9092";
    [Required] public string Topic { get; init; } = "orders.events";
}
