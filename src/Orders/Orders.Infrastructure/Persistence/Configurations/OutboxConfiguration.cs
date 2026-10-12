using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniShop.Orders.Infrastructure.Outbox;

namespace MiniShop.Orders.Infrastructure.Persistence.Configurations;

public sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.Topic).HasMaxLength(128).IsRequired();
        b.Property(x => x.Key).HasMaxLength(128).IsRequired();
        b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => x.SentAt);
    }
}
