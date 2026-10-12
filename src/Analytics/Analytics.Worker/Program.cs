using MiniShop.Analytics.Worker.ClickHouse;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter()));

builder.Services.Configure<MiniShop.Analytics.Worker.Kafka.KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<ClickHouseSettings>(
    builder.Configuration.GetSection("ClickHouse"));

builder.Services.AddSingleton<OrderEventsSink>();
builder.Services.AddHostedService<MiniShop.Analytics.Worker.Kafka.OrderEventsConsumer>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapHealthChecks("/health");

app.MapGet("/stats/orders-by-day", async (int days, OrderEventsSink sink, CancellationToken ct) =>
{
    days = Math.Clamp(days is 0 ? 7 : days, 1, 90);
    return Results.Ok(await sink.OrdersByDayAsync(days, ct));
}).WithTags("Stats");

app.Run();
