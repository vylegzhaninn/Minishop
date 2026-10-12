namespace MiniShop.Orders.Domain.Orders;

public sealed record OrderItem(string Sku, int Qty, long PriceMinor)
{
    public long LineTotal => checked(Qty * PriceMinor);
}
