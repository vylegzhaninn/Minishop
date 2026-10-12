using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniShop.Orders.Infrastructure.Persistence;

namespace MiniShop.Orders.Infrastructure.Outbox;

public sealed class OutboxPublisher(IServiceScopeFactory scopes, ILogger<OutboxPublisher> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {

        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
                var kafka = scope.ServiceProvider.GetRequiredService<Messaging.KafkaOrderPublisher>();

                var batch = await db.Outbox
                    .Where(m => m.SentAt == null)
                    .OrderBy(m => m.CreatedAt)
                    .Take(50)
                    .ToListAsync(ct);

                foreach (var msg in batch)
                {
                    await kafka.PublishRawAsync(msg.Key, msg.Payload, ct);
                    msg.SentAt = DateTimeOffset.UtcNow;
                }

                if (batch.Count > 0)
                    await db.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                log.LogError(ex, "Outbox publish failed, retry in 3s");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}
