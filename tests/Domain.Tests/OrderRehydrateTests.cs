using MiniShop.Orders.Domain.Orders;

namespace MiniShop.Domain.Tests;

// Регрессия на Critical #1 из ревью: кэш обязан возвращать тот же Id/Status/CreatedAt.
// Проверяем через Rehydrate — именно его использует CachedOrderRepository.
public sealed class OrderRehydrateTests
{
    [Fact]
    public void Rehydrate_preserves_identity_and_status()
    {
        var original = Order.Create(Guid.NewGuid(),
            [new OrderItem("coffee-250", 2, 349)], "RUB").Value!;
        original.MarkPaid();

        var copy = Order.Rehydrate(
            original.Id, original.CustomerId, original.Status,
            original.Currency, original.CreatedAt, original.Items.ToList());

        Assert.Equal(original.Id, copy.Id);
        Assert.Equal(OrderStatus.Paid, copy.Status);
        Assert.Equal(original.CreatedAt, copy.CreatedAt);
        Assert.Equal(698, copy.TotalMinor);
        Assert.Empty(copy.DomainEvents);
    }

    [Fact]
    public void Rehydrate_without_items_throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Order.Rehydrate(Guid.NewGuid(), Guid.NewGuid(), OrderStatus.Created,
                "RUB", DateTimeOffset.UtcNow, []));
    }

    [Fact]
    public void Money_mismatch_returns_result_not_exception()
    {
        var result = new Money(100, "RUB").TryAdd(new Money(100, "USD"));
        Assert.False(result.IsSuccess);
        Assert.Equal("validation", result.Error!.Code);
    }
}
