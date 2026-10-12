using MiniShop.Orders.Domain.Orders;
using MiniShop.SharedKernel;

namespace MiniShop.Orders.Domain.Orders;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken ct);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Order>> ListAsync(int take, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
