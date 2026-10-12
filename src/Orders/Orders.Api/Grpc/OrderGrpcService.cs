using Grpc.Core;
using MiniShop.Orders.Domain.Orders;
using MiniShop.Orders.Grpc;

namespace MiniShop.Orders.Api.Grpc;

public sealed class OrderGrpcService(IOrderRepository repo) : OrderService.OrderServiceBase
{
    public override async Task<GetOrderResponse> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "bad order_id"));

        var order = await repo.GetByIdAsync(id, context.CancellationToken);
        if (order is null)
            throw new RpcException(new Status(StatusCode.NotFound, "order not found"));

        return new GetOrderResponse
        {
            OrderId = order.Id.ToString(),
            CustomerId = order.CustomerId.ToString(),
            Status = order.Status.ToString(),
            TotalMinor = order.TotalMinor,
            Currency = order.Currency,
            ItemsCount = order.Items.Count,
        };
    }
}
