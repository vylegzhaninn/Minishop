using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniShop.Orders.Domain.Orders;
using StackExchange.Redis;

namespace MiniShop.Orders.Infrastructure.Caching;

public sealed class CachedOrderRepository(
    IOrderRepository inner,
    IConnectionMultiplexer redis,
    IOptions<RedisCacheSettings> settings,
    ILogger<CachedOrderRepository> log) : IOrderRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(settings.Value.TtlMinutes);

    public Task AddAsync(Order order, CancellationToken ct) => inner.AddAsync(order, ct);
    public Task SaveChangesAsync(CancellationToken ct) => inner.SaveChangesAsync(ct);

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = Key(id);
        try
        {
            var cached = await db.StringGetAsync(key);
            if (cached.HasValue)
                return JsonSerializer.Deserialize<CachedOrder>(cached!, Json)?.ToOrder();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Redis GET failed for {Key}, fallback to DB", key);
        }

        var order = await inner.GetByIdAsync(id, ct);
        if (order is not null)
        {
            try { await db.StringSetAsync(key, JsonSerializer.Serialize(CachedOrder.From(order), Json), _ttl); }
            catch (Exception ex) { log.LogWarning(ex, "Redis SET failed for {Key}", key); }
        }
        return order;
    }

    public Task<IReadOnlyList<Order>> ListAsync(int take, CancellationToken ct)
        => inner.ListAsync(take, ct); 

    public Task InvalidateAsync(Guid id)
        => redis.GetDatabase().KeyDeleteAsync(Key(id));

    private static string Key(Guid id) => $"order:{id}";

    private sealed record CachedOrder(Guid Id, Guid CustomerId, string Status, string Currency, DateTimeOffset CreatedAt, List<OrderItem> Items)
    {
        public static CachedOrder From(Order o) => new(o.Id, o.CustomerId, o.Status.ToString(), o.Currency, o.CreatedAt, [.. o.Items]);

        public Order ToOrder()
        {

            if (!Enum.TryParse<OrderStatus>(Status, out var status))
                throw new InvalidOperationException($"Cached order has bad status: '{Status}'");
            return Order.Rehydrate(Id, CustomerId, status, Currency, CreatedAt, Items);
        }
    }
}

public sealed class RedisCacheSettings
{
    public int TtlMinutes { get; init; } = 5;
}
