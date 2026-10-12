namespace MiniShop.Orders.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Topic { get; init; } = "";
    public string Key { get; init; } = "";
    public string Payload { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
}
