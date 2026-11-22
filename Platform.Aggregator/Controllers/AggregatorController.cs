using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
[Produces("application/json")]
public class AggregatorController(CachedOrderAggregationService aggregation, ILogger<AggregatorController> logger) : ControllerBase
{
    /// <summary>
    /// Повна картка замовлення: замовлення + товари + відгуки (+ попередження, якщо якийсь сервіс недоступний).
    /// Повна відповідь кешується в Redis на 30 с; <c>refresh=true</c> обходить кеш (для порівняння затримок HTTP і gRPC).
    /// </summary>
    [HttpGet("orders/{id:long}/full")]
    [ProducesResponseType<FullOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FullOrderDto>> GetFullOrder(long id, CancellationToken ct, [FromQuery] bool refresh = false)
    {
        try
        {
            var result = await aggregation.GetFullOrderAsync(id, refresh, ct);
            return result is null
                ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Resource not found", detail: $"Order {id} not found")
                : Ok(result);
        }
        catch (HttpRequestException ex)
        {
            // недоступний саме orders-api: без замовлення агрегувати нічого
            logger.LogError(ex, "orders-api is unavailable, order {OrderId} cannot be aggregated", id);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service unavailable",
                detail: "orders-api is unavailable");
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode is Grpc.Core.StatusCode.Unavailable or Grpc.Core.StatusCode.DeadlineExceeded)
        {
            logger.LogError(ex, "orders-api is unavailable ({Status}), order {OrderId} cannot be aggregated", ex.StatusCode, id);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service unavailable",
                detail: "orders-api is unavailable");
        }
    }

    /// <summary>Скидає кешовану агреговану відповідь (після змін у вихідних даних, коли не можна чекати 30 с).</summary>
    [HttpDelete("orders/{id:long}/cache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> InvalidateCache(long id, CancellationToken ct)
    {
        await aggregation.InvalidateAsync(id, ct);
        return NoContent();
    }
}
