using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using Catalog.Contracts;
using Catalog.Domain.Exceptions;
using Grpc.Core;
using ValidationException = Catalog.Domain.Exceptions.ValidationException;

// Простір імен НЕ Catalog.Api.Grpc: сегмент "Grpc" затьмарив би пакет Grpc.Core у всьому проєкті.
namespace Catalog.Api.GrpcServices;

/// <summary>
/// gRPC-адаптер над BLL (як контролер над HTTP): жодної бізнес-логіки. Використовує ТОЙ САМИЙ IProductService, що й
/// REST-контролер, тому кеш (L1 + Redis) працює для обох протоколів без дублювання коду.
/// Доменні винятки перетворюються на RpcException з відповідним статусом, а не на Unknown/Internal.
/// </summary>
public sealed class CatalogGrpcService(IProductService products, ILogger<CatalogGrpcService> logger)
    : CatalogService.CatalogServiceBase
{
    private const int MaxIdsPerCall = 200;

    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive"));

        try
        {
            var dto = await products.GetByIdAsync(request.Id, context.CancellationToken);   // з кешем
            return ToReply(dto);
        }
        catch (EntityNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<ProductsReply> GetProductsByIds(GetProductsByIdsRequest request, ServerCallContext context)
    {
        if (request.Ids.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "At least one id is required"));
        if (request.Ids.Count > MaxIdsPerCall)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"At most {MaxIdsPerCall} ids per call"));
        if (request.Ids.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Ids must be positive"));

        var ids = request.Ids.Distinct().ToList();
        var ct = context.CancellationToken;

        // кожен товар іде через кеш; незнайдені пропускаються — рішення про попередження приймає Aggregator
        var tasks = ids.Select(async id =>
        {
            try { return await products.GetByIdAsync(id, ct); }
            catch (EntityNotFoundException) { return null; }
        }).ToList();
        var found = await Task.WhenAll(tasks);

        var reply = new ProductsReply();
        reply.Products.AddRange(found.Where(p => p is not null).Select(p => ToReply(p!)));
        logger.LogDebug("gRPC GetProductsByIds: requested {Requested}, found {Found}", ids.Count, reply.Products.Count);
        return reply;
    }

    private static ProductReply ToReply(ProductDetailsDto p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Sku = p.Sku,
        Price = MoneyMapper.ToMoney(p.Price),
        AvailableStock = p.AvailableStock,
        IsActive = p.IsActive
    };
}

internal static class MoneyMapper
{
    private const decimal Nano = 1_000_000_000m;

    public static Money ToMoney(decimal value)
    {
        var units = decimal.Truncate(value);
        return new Money { Units = (long)units, Nanos = (int)decimal.Round((value - units) * Nano) };
    }
}
