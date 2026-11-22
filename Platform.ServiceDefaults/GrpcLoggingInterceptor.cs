using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Platform.ServiceDefaults;

/// <summary>
/// Серверний interceptor (Лаб. 06, ч. 6.2): кожен унарний gRPC-виклик логується структурованими властивостями —
/// метод, peer, статус, тривалість. Помилки домену мають бути перетворені на RpcException в адаптері (NotFound,
/// InvalidArgument); усе інше тут логується зі stack trace й повертається клієнту як Internal без внутрішніх деталей.
/// CorrelationId уже в LogContext (CorrelationIdMiddleware виконується для gRPC так само, як для будь-якого HTTP-запиту).
/// </summary>
public sealed class GrpcLoggingInterceptor(ILogger<GrpcLoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var started = Stopwatch.GetTimestamp();
        Activity.Current?.SetTag("rpc.peer", context.Peer);

        try
        {
            var response = await continuation(request, context);
            logger.LogInformation("gRPC {Method} from {Peer}: {Status} in {Elapsed} ms",
                context.Method, context.Peer, StatusCode.OK, Elapsed(started));
            return response;
        }
        catch (RpcException ex)
        {
            // очікувані бізнес-статуси (NotFound, InvalidArgument) — Warning; решта — Error
            var level = ex.StatusCode is StatusCode.NotFound or StatusCode.InvalidArgument or StatusCode.AlreadyExists
                ? LogLevel.Warning : LogLevel.Error;
            logger.Log(level, ex, "gRPC {Method} from {Peer}: {Status} in {Elapsed} ms: {Detail}",
                context.Method, context.Peer, ex.StatusCode, Elapsed(started), ex.Status.Detail);
            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("gRPC {Method} from {Peer}: {Status} in {Elapsed} ms (cancelled by client)",
                context.Method, context.Peer, StatusCode.Cancelled, Elapsed(started));
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "gRPC {Method} from {Peer}: {Status} in {Elapsed} ms",
                context.Method, context.Peer, StatusCode.Internal, Elapsed(started));
            throw new RpcException(new Status(StatusCode.Internal, "Internal error"));
        }
    }

    private static string Elapsed(long started) => Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F1");
}
