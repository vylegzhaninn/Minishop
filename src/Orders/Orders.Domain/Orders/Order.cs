using MiniShop.Orders.Domain.Events;
using MiniShop.SharedKernel;

namespace MiniShop.Orders.Domain.Orders;

public sealed class Order : Entity
{
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Created;
    public string Currency { get; private set; } = "RUB";
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public long TotalMinor => _items.Sum(i => i.LineTotal);

    private Order() { }

    public static Order Rehydrate(Guid id, Guid customerId, OrderStatus status, string currency, DateTimeOffset createdAt, IEnumerable<OrderItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        var list = items.ToList();
        if (list.Count == 0)
            throw new InvalidOperationException("Cannot rehydrate order without items");
        var order = new Order
        {
            Id = id,
            CustomerId = customerId,
            Status = status,
            Currency = currency,
            CreatedAt = createdAt,
        };
        order._items.AddRange(list);
        return order;
    }

    private Order(Guid customerId, string currency, IEnumerable<OrderItem> items)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Currency = currency;
        CreatedAt = DateTimeOffset.UtcNow;
        _items.AddRange(items);
    }

    public static Result<Order> Create(Guid customerId, IReadOnlyCollection<OrderItem> items, string currency = "RUB")
    {
        if (customerId == Guid.Empty)
            return Result<Order>.Fail(Error.Validation("customerId is empty"));
        if (items is null || items.Count == 0)
            return Result<Order>.Fail(Error.Validation("order must contain at least 1 item"));
        if (items.Count > 100)
            return Result<Order>.Fail(Error.Validation("too many items (max 100)"));
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return Result<Order>.Fail(Error.Validation("currency must be ISO-4217 (3 letters)"));

        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.Sku) || it.Sku.Length > 64)
                return Result<Order>.Fail(Error.Validation($"bad sku: '{it.Sku}'"));
            if (it.Qty <= 0 || it.Qty > 999)
                return Result<Order>.Fail(Error.Validation($"bad qty for {it.Sku}"));
            if (it.PriceMinor < 0)
                return Result<Order>.Fail(Error.Validation($"negative price for {it.Sku}"));
        }

        var order = new Order(customerId, currency.ToUpperInvariant(), items);
        order.Raise(new OrderCreatedEvent(order.Id, customerId, order.TotalMinor, currency, DateTimeOffset.UtcNow));
        return Result<Order>.Ok(order);
    }

    public Result<Order> MarkPaid()
    {
        if (Status != OrderStatus.Created)
            return Result<Order>.Fail(Error.Conflict($"cannot pay order in status {Status}"));
        Status = OrderStatus.Paid;
        return Result<Order>.Ok(this);
    }
}
