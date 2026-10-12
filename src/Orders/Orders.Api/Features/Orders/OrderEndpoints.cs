using System.Text.Json;
using Microsoft.Extensions.Options;
using MiniShop.Orders.Domain.Orders;
using MiniShop.Orders.Infrastructure.Outbox;
using MiniShop.Orders.Infrastructure.Persistence;
using MiniShop.SharedKernel;

namespace MiniShop.Orders.Api.Features.Orders;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/orders").WithTags("Orders");

        g.MapPost("/", CreateAsync)
            .WithName("CreateOrder")
            .Produces<OrderResponse>(201)
            .ProducesProblem(400);

        g.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetOrder")
            .Produces<OrderResponse>(200)
            .ProducesProblem(404);

        g.MapGet("/", ListAsync)
            .WithName("ListOrders")
            .Produces<IReadOnlyList<OrderResponse>>(200);
    }

    private static async Task<IResult> CreateAsync(
        CreateOrderRequest req, IOrderRepository repo, OrdersDbContext db,
        IOptions<KafkaOptions> kafka, CancellationToken ct)
    {
        var handler = new CreateOrderHandler(repo, db, kafka);
        var result = await handler.HandleAsync(req, ct);

        return result.Match<IResult>(
            onOk: o => Results.Created($"/orders/{o.Id}", o),
            onFail: e => Results.BadRequest(new { error = e.Code, message = e.Message }));
    }

    private static async Task<IResult> GetByIdAsync(Guid id, IOrderRepository repo, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(id, ct);
        return order is null
            ? Results.NotFound(new { error = "not_found", message = $"order {id} not found" })
            : Results.Ok(OrderResponse.From(order));
    }

    private static async Task<IResult> ListAsync(int take, IOrderRepository repo, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 100);
        var orders = await repo.ListAsync(take, ct);
        return Results.Ok(orders.Select(OrderResponse.From).ToList());
    }
}

public sealed record CreateOrderRequest(Guid CustomerId, List<CreateOrderItem> Items, string Currency = "RUB");
public sealed record CreateOrderItem(string Sku, int Qty, long PriceMinor);

public sealed record OrderResponse(Guid Id, Guid CustomerId, string Status, long TotalMinor, string Currency, DateTimeOffset CreatedAt)
{
    public static OrderResponse From(Order o)
        => new(o.Id, o.CustomerId, o.Status.ToString(), o.TotalMinor, o.Currency, o.CreatedAt);
}

public sealed class CreateOrderHandler(IOrderRepository repo, OrdersDbContext db, IOptions<KafkaOptions> kafka)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<OrderResponse>> HandleAsync(CreateOrderRequest req, CancellationToken ct)
    {
        var items = req.Items.Select(i => new OrderItem(i.Sku.Trim(), i.Qty, i.PriceMinor)).ToList();
        var created = Order.Create(req.CustomerId, items, req.Currency);
        if (!created.IsSuccess)
            return Result<OrderResponse>.Fail(created.Error!);

        var order = created.Value!;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await repo.AddAsync(order, ct);

        foreach (var e in order.DomainEvents)
        {
            db.Outbox.Add(new OutboxMessage
            {
                Topic = kafka.Value.Topic,
                Key = order.Id.ToString(), 
                Payload = JsonSerializer.Serialize(new OrderEventEnvelope(
                    e.EventId, "order.created", order.Id, order.CustomerId,
                    order.TotalMinor, order.Currency, e.OccurredAt), Json),
            });
        }

        order.ClearEvents();
        await repo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<OrderResponse>.Ok(OrderResponse.From(order));
    }

    private sealed record OrderEventEnvelope(
        Guid EventId, string EventType, Guid OrderId, Guid CustomerId,
        long TotalMinor, string Currency, DateTimeOffset OccurredAt);
}
