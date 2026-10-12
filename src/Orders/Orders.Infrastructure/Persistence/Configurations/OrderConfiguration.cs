using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniShop.Orders.Domain.Orders;

namespace MiniShop.Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        b.Property<List<OrderItem>>("_items")
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("items")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<OrderItem>>(v, Json) ?? []);

        b.Ignore(x => x.Items);
        b.Ignore(x => x.TotalMinor);
        b.Ignore(x => x.DomainEvents);

        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.CreatedAt);
    }
}
