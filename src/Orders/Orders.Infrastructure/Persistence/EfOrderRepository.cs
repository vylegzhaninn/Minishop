using Microsoft.EntityFrameworkCore;
using MiniShop.Orders.Domain.Orders;
using MiniShop.Orders.Infrastructure.Persistence;

namespace MiniShop.Orders.Infrastructure.Persistence;

public sealed class EfOrderRepository(OrdersDbContext db) : IOrderRepository
{
    public Task AddAsync(Order order, CancellationToken ct) => db.Orders.AddAsync(order, ct).AsTask();

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Order>> ListAsync(int take, CancellationToken ct)
        => await db.Orders.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
