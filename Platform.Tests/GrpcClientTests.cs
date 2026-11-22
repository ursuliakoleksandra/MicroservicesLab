using Catalog.Contracts;
using Grpc.Core;
using NSubstitute;
using Orders.Contracts;
using Platform.Aggregator.Clients;
using Xunit;
using Google.Protobuf.WellKnownTypes;

namespace Platform.Tests;

/// <summary>gRPC-клієнти Aggregator: відображення повідомлень у DTO та обробка статусів RpcException.</summary>
public class GrpcClientTests
{
    private static AsyncUnaryCall<T> Ok<T>(T reply) => new(Task.FromResult(reply), Task.FromResult(new Metadata()),
        () => Status.DefaultSuccess, () => new Metadata(), () => { });

    private static AsyncUnaryCall<T> Fail<T>(StatusCode code) => new(Task.FromException<T>(new RpcException(new Status(code, "x"))),
        Task.FromResult(new Metadata()), () => new Status(code, "x"), () => new Metadata(), () => { });

    [Fact]
    public async Task Orders_Maps_MoneyTimestampAndOptionalComment()
    {
        var reply = new OrderReply
        {
            Id = 5, CustomerId = 2, Status = "Paid", ShippingAddress = "Чернівці",
            OrderDate = Timestamp.FromDateTime(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)),
            Total = new Orders.Contracts.Money { Units = 100, Nanos = 500_000_000 }
        };
        reply.Items.Add(new OrderItemReply { ProductId = 3, ProductName = "X", Units = 2,
            UnitPrice = new Orders.Contracts.Money { Units = 50, Nanos = 250_000_000 } });

        var grpc = Substitute.For<OrdersService.OrdersServiceClient>();
        grpc.GetOrderAsync(Arg.Any<GetOrderRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>()).Returns(Ok(reply));

        var order = await new GrpcOrdersClient(grpc).GetOrderAsync(5, CancellationToken.None);

        Assert.NotNull(order);
        Assert.Equal(100.5m, order.Total);
        Assert.Null(order.Comment);                                      // optional-поле не задано
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), order.OrderDate);
        Assert.Equal(50.25m, order.Items[0].UnitPrice);
    }

    [Fact]
    public async Task Orders_NotFound_BecomesNull()
    {
        var grpc = Substitute.For<OrdersService.OrdersServiceClient>();
        grpc.GetOrderAsync(Arg.Any<GetOrderRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Fail<OrderReply>(StatusCode.NotFound));

        Assert.Null(await new GrpcOrdersClient(grpc).GetOrderAsync(404, CancellationToken.None));
    }

    [Fact]
    public async Task Catalog_Unavailable_PropagatesAsRpcException_ForPartialResponseHandling()
    {
        var grpc = Substitute.For<CatalogService.CatalogServiceClient>();
        grpc.GetProductsByIdsAsync(Arg.Any<GetProductsByIdsRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Fail<ProductsReply>(StatusCode.Unavailable));

        var ex = await Assert.ThrowsAsync<RpcException>(() => new GrpcCatalogClient(grpc).GetProductsAsync([1, 2], CancellationToken.None));
        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact]
    public async Task Catalog_UsesSingleBatchCall_AndMapsPrice()
    {
        var reply = new ProductsReply();
        reply.Products.Add(new ProductReply { Id = 1, Name = "N", Sku = "S", AvailableStock = 3, IsActive = true,
            Price = new Catalog.Contracts.Money { Units = 32999, Nanos = 990_000_000 } });
        var grpc = Substitute.For<CatalogService.CatalogServiceClient>();
        grpc.GetProductsByIdsAsync(Arg.Any<GetProductsByIdsRequest>(), Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Ok(reply));

        var products = await new GrpcCatalogClient(grpc).GetProductsAsync([1, 2, 3], CancellationToken.None);

        Assert.Equal(32999.99m, products.Single().Price);
        await grpc.Received(1).GetProductsByIdsAsync(Arg.Is<GetProductsByIdsRequest>(r => r.Ids.Count == 3),
            Arg.Any<Metadata>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }
}
