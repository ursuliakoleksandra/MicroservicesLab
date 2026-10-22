using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController(ICategoryService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById(long id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    /// <summary>Категорія з товарами (Explicit Loading: категорія — 1-й SQL-запит, товари — 2-й).</summary>
    [HttpGet("{id:long}/products")]
    [ProducesResponseType<CategoryWithProductsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryWithProductsDto>> GetWithProducts(long id, CancellationToken ct)
        => Ok(await service.GetWithProductsAsync(id, ct));

    /// <summary>Товари, додані в категорію після дати since (LINQ M:N за полем AddedAt проміжної сутності).</summary>
    [HttpGet("recent-products")]
    [ProducesResponseType<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetRecentProducts(
        [FromQuery] string categoryName, CancellationToken ct, [FromQuery] DateTime? since = null)
        => Ok(await service.GetRecentlyAddedProductsAsync(categoryName, since, ct));

    [HttpPost]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create(
        CreateCategoryDto input, [FromServices] IValidator<CreateCategoryDto> validator, CancellationToken ct)
    {
        if (await validator.ValidateToProblemAsync(input, ct) is { } problem) return ValidationProblem(problem);

        var created = await service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
