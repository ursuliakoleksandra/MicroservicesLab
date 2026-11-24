using System.Diagnostics;
using MediatR;
using Reviews.Application.Reviews;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.DeleteReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Commands.CreateReview;

namespace Reviews.WebApi.Metrics;

/// <summary>
/// MediatR-behavior: тривалість КОЖНОГО запиту (успішного й невдалого) та бізнес-лічильники після успіху. Працює і для REST, і для gRPC,
/// бо обидва адаптери надсилають ті самі команди та запити.
/// </summary>
public sealed class MetricsBehavior<TRequest, TResponse>(ReviewMetrics metrics) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        metrics.OperationStarted();
        var sw = Stopwatch.StartNew();
        var success = false;
        try
        {
            var response = await next();
            success = true;
            CountBusinessEvent(request, response);
            return response;
        }
        finally
        {
            metrics.OperationFinished(typeof(TRequest).Name, success, sw.Elapsed.TotalMilliseconds);
        }
    }

    private void CountBusinessEvent(TRequest request, TResponse response)
    {
        switch (request)
        {
            case CreateReviewCommand when response is ReviewDto dto: metrics.ReviewCreated(dto.Type); break;
            case UpdateReviewCommand: metrics.ReviewUpdated(); break;
            case DeleteReviewCommand: metrics.ReviewDeleted(); break;
            case AddCommentCommand: metrics.CommentAdded(); break;
        }
    }
}
