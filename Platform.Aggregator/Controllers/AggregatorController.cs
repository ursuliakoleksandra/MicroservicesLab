using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
[Produces("application/json")]
public class AggregatorController(IOrderAggregationService aggregation, ILogger<AggregatorController> logger) : ControllerBase
{
    /// <summary>Повна картка замовлення: замовлення + товари + відгуки (+ попередження, якщо якийсь сервіс недоступний).</summary>
    [HttpGet("orders/{id:long}/full")]
    [ProducesResponseType<FullOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FullOrderDto>> GetFullOrder(long id, CancellationToken ct)
    {
        try
        {
            var result = await aggregation.GetFullOrderAsync(id, ct);
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
    }
}
