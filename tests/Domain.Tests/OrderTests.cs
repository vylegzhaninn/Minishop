using MiniShop.Orders.Domain.Orders;

namespace MiniShop.Domain.Tests;

// Пример: домен тестируется без базы/моков — фабрика чистая.
public sealed class OrderTests
{
    [Fact]
    public void Create_with_empty_items_fails()
    {
        var result = Order.Create(Guid.NewGuid(), [], "RUB");
        Assert.False(result.IsSuccess);
        Assert.Equal("validation", result.Error!.Code);
    }

    [Fact]
    public void Create_computes_total()
    {
        var result = Order.Create(Guid.NewGuid(),
            [new OrderItem("coffee-250", 2, 349)], "RUB");
        Assert.True(result.IsSuccess);
        Assert.Equal(698, result.Value!.TotalMinor);
        Assert.Single(result.Value.DomainEvents);
    }

    [Fact]
    public void Double_pay_is_conflict()
    {
        var order = Order.Create(Guid.NewGuid(),
            [new OrderItem("sku", 1, 100)], "RUB").Value!;
        Assert.True(order.MarkPaid().IsSuccess);
        var second = order.MarkPaid();
        Assert.False(second.IsSuccess);
        Assert.Equal("conflict", second.Error!.Code);
    }
}
