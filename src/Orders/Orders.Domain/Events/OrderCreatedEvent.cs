using MiniShop.SharedKernel;

namespace MiniShop.Orders.Domain.Events;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    long TotalMinor,
    string Currency,
    DateTimeOffset OccurredAt
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}
