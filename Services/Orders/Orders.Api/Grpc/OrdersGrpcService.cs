using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Orders.Bll.Dtos;
using Orders.Bll.Services;
using Orders.Contracts;
using Orders.Domain.Exceptions;
using ValidationException = Orders.Domain.Exceptions.ValidationException;

// Простір імен НЕ Orders.Api.Grpc: сегмент "Grpc" затьмарив би пакет Grpc.Core у всьому проєкті.
namespace Orders.Api.GrpcServices;

/// <summary>gRPC-адаптер над BLL замовлень (той самий IOrderService, що й у REST-контролера). Винятки домену -> RpcException.</summary>
public sealed class OrdersGrpcService(IOrderService orders) : OrdersService.OrdersServiceBase
{
    public override async Task<OrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive"));

        try
        {
            return ToReply(await orders.GetByIdAsync(request.Id, context.CancellationToken));
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    private static OrderReply ToReply(OrderDto o)
    {
        var reply = new OrderReply
        {
            Id = o.Id,
            CustomerId = o.CustomerId,
            Status = o.Status,
            OrderDate = Timestamp.FromDateTime(DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc)),
            ShippingAddress = o.ShippingAddress,
            Total = ToMoney(o.Total),
            RowVersion = o.RowVersion
        };
        if (o.Comment is not null) reply.Comment = o.Comment;     // optional-поле: задаємо лише за наявності

        reply.Items.AddRange(o.Items.Select(i => new OrderItemReply
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            UnitPrice = ToMoney(i.UnitPrice),
            Discount = ToMoney(i.Discount),
            Units = i.Units,
            LineTotal = ToMoney(i.LineTotal)
        }));
        return reply;
    }

    private static Money ToMoney(decimal value)
    {
        var units = decimal.Truncate(value);
        return new Money { Units = (long)units, Nanos = (int)decimal.Round((value - units) * 1_000_000_000m) };
    }
}
