using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers;

/// <summary>Тонкий адаптер HTTP -> BLL: жодного SQL і жодних бізнес-правил. Помилки обробляє глобальний обробник.</summary>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController(IOrderService orders) : ControllerBase
{
    /// <summary>Замовлення з позиціями. Поле rowVersion потрібне для PUT .../details.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(long id, CancellationToken ct)
        => Ok(await orders.GetByIdAsync(id, ct));                      // NotFound -> 404 обробником

    /// <summary>Замовлення клієнта (фільтр за статусом, пагінація).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResultDto<OrderSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResultDto<OrderSummaryDto>>> GetByCustomer(
        [FromQuery, Range(1, long.MaxValue)] long customerId,
        [FromQuery] string? status,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await orders.GetCustomerOrdersAsync(customerId, status, page, pageSize, ct));

    /// <summary>Історія змін статусу.</summary>
    [HttpGet("{id:long}/history")]
    [ProducesResponseType<IReadOnlyList<OrderStatusHistoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrderStatusHistoryDto>>> GetHistory(long id, CancellationToken ct)
        => Ok(await orders.GetHistoryAsync(id, ct));

    /// <summary>Створення замовлення (назва й ціна товару фіксуються як знімок).</summary>
    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto input, CancellationToken ct)
    {
        var created = await orders.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);   // 201 + Location
    }

    /// <summary>Зміна адреси/коментаря з оптимістичною конкурентністю (rowVersion).</summary>
    [HttpPut("{id:long}/details")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> UpdateDetails(long id, UpdateOrderDetailsDto input, CancellationToken ct)
        => Ok(await orders.UpdateDetailsAsync(id, input, ct));

    /// <summary>М'яке видалення (лише Submitted / Cancelled).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await orders.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Підтвердити замовлення: статус + залишки товарів в одній транзакції.</summary>
    [HttpPost("{id:long}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(long id, CancellationToken ct)
    {
        await orders.ConfirmOrderAsync(id, ct);
        return NoContent();
    }

    /// <summary>Оплатити підтверджене замовлення.</summary>
    [HttpPost("{id:long}/pay")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Pay(long id, PayOrderDto input, CancellationToken ct)
    {
        await orders.PayOrderAsync(id, input, ct);
        return NoContent();
    }

    /// <summary>Відвантажити (лише оплачене замовлення, інакше 409).</summary>
    [HttpPost("{id:long}/ship")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Ship(long id, CancellationToken ct)
    {
        await orders.ShipOrderAsync(id, ct);
        return NoContent();
    }

    /// <summary>Скасувати (лише до оплати; залишки повертаються).</summary>
    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        await orders.CancelOrderAsync(id, ct);
        return NoContent();
    }
}
