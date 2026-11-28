using Platform.Security;
using Platform.Security.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers;

/// <summary>Локальний довідник товарів сервісу Замовлень (власник назви/ціни — Каталог).</summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController(IProductService products) : ControllerBase
{
    [RequirePermission(Permissions.Orders.Read)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll(CancellationToken ct)
        => Ok(await products.GetAllAsync(ct));

    [RequirePermission(Permissions.Orders.Read)]
    [HttpGet("{id:long}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(long id, CancellationToken ct)
        => Ok(await products.GetByIdAsync(id, ct));

    /// <summary>Синхронізація копії з Каталогу (імітація події ProductChanged; у Лаб. 08 — справжні події).</summary>
    [RequirePermission(Permissions.Orders.Update)]
    [HttpPut("{id:long}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Sync(long id, SyncProductDto input, CancellationToken ct)
        => Ok(await products.SyncFromCatalogAsync(id, input, ct));
}
