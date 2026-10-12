using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniShop.Orders.Api.Features.Orders;
using MiniShop.Orders.Api.Grpc;
using MiniShop.Orders.Api.Infrastructure;
using MiniShop.Orders.Domain.Orders;
using MiniShop.Orders.Infrastructure.Caching;
using MiniShop.Orders.Infrastructure.Messaging;
using MiniShop.Orders.Infrastructure.Options;
using MiniShop.Orders.Infrastructure.Outbox;
using MiniShop.Orders.Infrastructure.Persistence;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter()));

builder.Services.AddOptions<KafkaOptions>()
    .Bind(builder.Configuration.GetSection(KafkaOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddDbContext<OrdersDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.Configure<RedisCacheSettings>(
    builder.Configuration.GetSection("RedisCache"));

builder.Services.AddScoped<EfOrderRepository>();
builder.Services.AddScoped<IOrderRepository>(sp =>
    new CachedOrderRepository(
        sp.GetRequiredService<EfOrderRepository>(),
        sp.GetRequiredService<IConnectionMultiplexer>(),
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisCacheSettings>>(),
        sp.GetRequiredService<ILogger<CachedOrderRepository>>()));

builder.Services.AddSingleton<KafkaOrderPublisher>();
builder.Services.AddHostedService<OutboxPublisher>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGrpc();
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

app.MapHealthChecks("/health");
app.MapOrderEndpoints();
app.MapGrpcService<OrderGrpcService>();

app.Run();
