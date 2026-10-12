using Microsoft.EntityFrameworkCore;
using MiniShop.Orders.Domain.Orders;
using MiniShop.Orders.Infrastructure.Outbox;
using MiniShop.Orders.Infrastructure.Persistence.Configurations;

namespace MiniShop.Orders.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfiguration(new OrderConfiguration());
        b.ApplyConfiguration(new OutboxConfiguration());
    }
}
