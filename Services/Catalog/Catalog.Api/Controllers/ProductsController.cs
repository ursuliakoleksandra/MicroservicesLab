using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

/// <summary>Тонкий адаптер HTTP -> BLL. До DbContext не звертається; помилки обробляє глобальний обробник.</summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController(IProductService service) : ControllerBase
{
    /// <summary>
    /// Список товарів: пагінація, фільтри (search, minPrice, maxPrice, categoryId, brandId, inStock) і сортування
    /// (sortBy = name|price|sku|created|stock, sortDir = asc|desc) виконуються в SQL.
    /// Метадані пагінації — у тілі відповіді та в заголовках X-Total-Count / X-Total-Pages / X-Page / X-Page-Size.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetList(
        [FromQuery] ProductQuery query,
        [FromServices] IValidator<ProductQuery> validator,
        CancellationToken ct)
    {
        if (await validator.ValidateToProblemAsync(query, ct) is { } problem) return ValidationProblem(problem);

        var result = await service.GetListAsync(query, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        Response.Headers["X-Total-Pages"] = result.TotalPages.ToString();
        Response.Headers["X-Page"] = result.Page.ToString();
        Response.Headers["X-Page-Size"] = result.PageSize.ToString();
        return Ok(result);
    }

    /// <summary>Товари, залишок яких не перевищує поріг (інша специфікація, той самий репозиторій).</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType<IReadOnlyList<ProductListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProductListItemDto>>> GetLowStock(
        CancellationToken ct, [FromQuery] int threshold = 5)
        => Ok(await service.GetLowStockAsync(threshold, ct));

    /// <summary>Картка товару (Eager Loading: бренд, деталі, зображення, категорії — один SQL-запит).</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetById(long id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));                                  // не знайдено -> 404

    [HttpPost]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create(
        CreateProductDto input, [FromServices] IValidator<CreateProductDto> validator, CancellationToken ct)
    {
        if (await validator.ValidateToProblemAsync(input, ct) is { } problem) return ValidationProblem(problem);

        var created = await service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);   // 201 + Location
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> Update(
        long id, UpdateProductDto input, [FromServices] IValidator<UpdateProductDto> validator, CancellationToken ct)
    {
        if (await validator.ValidateToProblemAsync(input, ct) is { } problem) return ValidationProblem(problem);
        return Ok(await service.UpdateAsync(id, input, ct));
    }

    /// <summary>Зміна ціни (ціна &gt; 0 — інваріант сутності).</summary>
    [HttpPut("{id:long}/price")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> ChangePrice(
        long id, ChangePriceDto input, [FromServices] IValidator<ChangePriceDto> validator, CancellationToken ct)
    {
        if (await validator.ValidateToProblemAsync(input, ct) is { } problem) return ValidationProblem(problem);
        return Ok(await service.ChangePriceAsync(id, input, ct));
    }

    /// <summary>Поповнення залишку.</summary>
    [HttpPost("{id:long}/stock/add")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> AddStock(long id, StockChangeDto input, CancellationToken ct)
        => Ok(await service.AddStockAsync(id, input, ct));

    /// <summary>Списання залишку; якщо товару менше, ніж запитано — 409 (правило сутності).</summary>
    [HttpPost("{id:long}/stock/remove")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> RemoveStock(long id, StockChangeDto input, CancellationToken ct)
        => Ok(await service.RemoveStockAsync(id, input, ct));

    /// <summary>Деталі товару (1:1).</summary>
    [HttpPut("{id:long}/detail")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> SaveDetail(
        long id, SaveProductDetailDto input, [FromServices] IValidator<SaveProductDetailDto> validator, CancellationToken ct)
    {
        if (await validator.ValidateToProblemAsync(input, ct) is { } problem) return ValidationProblem(problem);
        return Ok(await service.SaveDetailAsync(id, input, ct));
    }

    /// <summary>Додати зображення (1:N).</summary>
    [HttpPost("{id:long}/images")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDetailsDto>> AddImage(
        long id, AddProductImageDto input, [FromServices] IValidator<AddProductImageDto> validator, CancellationToken ct)
    {
        if (await validator.ValidateToProblemAsync(input, ct) is { } problem) return ValidationProblem(problem);
        return Ok(await service.AddImageAsync(id, input, ct));
    }

    /// <summary>Додати товар до категорії (M:N; AddedAt проставляється автоматично).</summary>
    [HttpPost("{id:long}/categories/{categoryId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignCategory(long id, long categoryId, CancellationToken ct)
    {
        await service.AssignCategoryAsync(id, categoryId, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}/categories/{categoryId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCategory(long id, long categoryId, CancellationToken ct)
    {
        await service.RemoveCategoryAsync(id, categoryId, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
