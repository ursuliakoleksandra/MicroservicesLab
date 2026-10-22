using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/brands")]
[Produces("application/json")]
public class BrandsController(IBrandService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BrandDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    [HttpPost]
    [ProducesResponseType<BrandDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BrandDto>> Create(
        CreateBrandDto input, [FromServices] IValidator<CreateBrandDto> validator, CancellationToken ct)
    {
        if (await validator.ValidateToProblemAsync(input, ct) is { } problem) return ValidationProblem(problem);

        var created = await service.CreateAsync(input, ct);
        return Created($"/api/brands/{created.Id}", created);        // окремого GET за Id немає — Location вказує на ресурс
    }
}
