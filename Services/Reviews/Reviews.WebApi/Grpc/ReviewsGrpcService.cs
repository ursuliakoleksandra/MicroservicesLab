using FluentValidation;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Reviews.Application.Reviews;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Contracts;
using Reviews.Domain.Exceptions;

// Простір імен НЕ Reviews.WebApi.Grpc: сегмент "Grpc" затьмарив би пакет Grpc.Core у всьому проєкті.
namespace Reviews.WebApi.GrpcServices;

/// <summary>
/// gRPC-адаптер над Application-шаром: той самий MediatR-запит GetReviewsListQuery, що й у REST-контролера
/// (валідація й логування pipeline працюють без змін). Винятки -> RpcException з відповідним статусом.
/// </summary>
public sealed class ReviewsGrpcService(ISender sender) : ReviewsService.ReviewsServiceBase
{
    private const int DefaultPerProduct = 5, MaxPerProduct = 20, MaxProducts = 100;

    public override async Task<ReviewsReply> GetForProducts(GetForProductsRequest request, ServerCallContext context)
    {
        if (request.ProductIds.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "At least one product id is required"));
        if (request.ProductIds.Count > MaxProducts)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"At most {MaxProducts} product ids per call"));
        if (request.ProductIds.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Product ids must be positive"));
        if (request.PerProduct is < 0 or > MaxPerProduct)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"per_product must be between 0 and {MaxPerProduct}"));

        var perProduct = request.PerProduct == 0 ? DefaultPerProduct : request.PerProduct;
        var ct = context.CancellationToken;

        try
        {
            // незалежні запити до MongoDB — паралельно
            var pages = await Task.WhenAll(request.ProductIds.Distinct().Select(productId =>
                sender.Send(new GetReviewsListQuery(productId, 1, perProduct, null, null, "newest"), ct)));

            var reply = new ReviewsReply();
            reply.Reviews.AddRange(pages.SelectMany(p => p.Items).Select(ToReply));
            return reply;
        }
        catch (ValidationException ex)                      // FluentValidation (ValidationBehavior)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    private static ReviewReply ToReply(ReviewDto r)
    {
        var reply = new ReviewReply
        {
            Id = r.Id,
            ProductId = r.ProductId,
            AuthorId = r.AuthorId,
            AuthorName = r.AuthorName,
            Type = r.Type,
            Rating = r.Rating,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)),
            CommentsCount = r.CommentsCount
        };
        if (r.Text is not null) reply.Text = r.Text;
        return reply;
    }
}
