using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;
using Microsoft.Extensions.Options;
using MiniShop.Analytics.Worker.Kafka;

namespace MiniShop.Analytics.Worker.ClickHouse;

public sealed class OrderEventsSink(IOptions<ClickHouseSettings> settings, ILogger<OrderEventsSink> log)
{
    private string ConnString =>
        $"Host={settings.Value.Host};Port={settings.Value.Port};Database={settings.Value.Database};" +
        $"Username={settings.Value.Username};Password={settings.Value.Password}";

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var conn = new ClickHouseConnection(ConnString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS order_events (
              event_id UUID,
              event_type String,
              order_id UUID,
              customer_id UUID,
              total_minor Int64,
              currency LowCardinality(String),
              occurred_at DateTime64(3, 'UTC'),
              _ingested_at DateTime64(3, 'UTC') DEFAULT now64()
            ) ENGINE = ReplacingMergeTree(_ingested_at)
            PARTITION BY toYYYYMM(occurred_at)
            ORDER BY (occurred_at, order_id, event_id)
            """;
        await cmd.ExecuteNonQueryAsync(ct);
        log.LogInformation("ClickHouse schema ensured");
    }

    public async Task InsertBatchAsync(IReadOnlyCollection<OrderEvent> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        await using var conn = new ClickHouseConnection(ConnString);
        await conn.OpenAsync(ct);
        foreach (var e in batch)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO order_events (event_id, event_type, order_id, customer_id, total_minor, currency, occurred_at)
                VALUES (@event_id, @event_type, @order_id, @customer_id, @total_minor, @currency, @occurred_at)
                """;
            cmd.Parameters.AddWithValue("event_id", e.EventId);
            cmd.Parameters.AddWithValue("event_type", e.EventType);
            cmd.Parameters.AddWithValue("order_id", e.OrderId);
            cmd.Parameters.AddWithValue("customer_id", e.CustomerId);
            cmd.Parameters.AddWithValue("total_minor", e.TotalMinor);
            cmd.Parameters.AddWithValue("currency", e.Currency);
            cmd.Parameters.AddWithValue("occurred_at", e.OccurredAt.UtcDateTime);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        log.LogInformation("Inserted {Count} events to ClickHouse", batch.Count);
    }

    public async Task<IReadOnlyList<OrdersByDayRow>> OrdersByDayAsync(int days, CancellationToken ct)
    {
        await using var conn = new ClickHouseConnection(ConnString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT toDate(occurred_at) AS day, count() AS orders, sum(total_minor) AS revenue_minor
            FROM order_events FINAL
            WHERE occurred_at >= now() - INTERVAL @days DAY
            GROUP BY day ORDER BY day
            """;
        cmd.Parameters.AddWithValue("days", days);
        var rows = new List<OrdersByDayRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            rows.Add(new OrdersByDayRow(r.GetDateTime(0), r.GetInt64(1), r.GetInt64(2)));
        return rows;
    }
}

public sealed class ClickHouseSettings
{
    public string Host { get; init; } = "clickhouse";
    public int Port { get; init; } = 8123;
    public string Database { get; init; } = "minishop";
    public string Username { get; init; } = "minishop";
    public string Password { get; init; } = "minishop";
}

public sealed record OrdersByDayRow(DateTime Day, long Orders, long RevenueMinor);
