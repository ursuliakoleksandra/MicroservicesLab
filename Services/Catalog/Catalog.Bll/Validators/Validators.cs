using System.Text.Json;
using Catalog.Bll.Dtos;
using Catalog.Domain.Filters;
using FluentValidation;

namespace Catalog.Bll.Validators;

// Складні правила та формат — FluentValidation у ОКРЕМИХ класах. Реєструються через AddValidatorsFromAssemblyContaining
// і викликаються ЯВНО (без застарілого AddFluentValidation) у контролерах.

public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name must not be blank");
        RuleFor(x => x.Sku).NotEmpty().Matches("^[A-Z0-9-]+$").WithMessage("SKU may contain only A-Z, 0-9 and '-'");
        RuleFor(x => x.Price).PriceRules();
    }
}

public class UpdateProductDtoValidator : AbstractValidator<UpdateProductDto>
{
    public UpdateProductDtoValidator() =>
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name must not be blank");
}

public class ChangePriceDtoValidator : AbstractValidator<ChangePriceDto>
{
    public ChangePriceDtoValidator() => RuleFor(x => x.Price).PriceRules();
}

public class SaveProductDetailDtoValidator : AbstractValidator<SaveProductDetailDto>
{
    public SaveProductDetailDtoValidator()
    {
        RuleFor(x => x.WeightGrams).GreaterThan(0).When(x => x.WeightGrams is not null);
        RuleFor(x => x.Specs).Must(BeValidJson).When(x => !string.IsNullOrWhiteSpace(x.Specs))
            .WithMessage("Specs must be valid JSON");
    }

    private static bool BeValidJson(string? json)
    {
        try { using var _ = JsonDocument.Parse(json!); return true; }
        catch (JsonException) { return false; }
    }
}

public class AddProductImageDtoValidator : AbstractValidator<AddProductImageDto>
{
    public AddProductImageDtoValidator() =>
        RuleFor(x => x.Url).Must(u => u.StartsWith('/') || Uri.TryCreate(u, UriKind.Absolute, out var uri)
                                      && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Url must be an absolute http(s) URL or a path starting with '/'");
}

public class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryDtoValidator()
    {
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name must not be blank");
        RuleFor(x => x.Slug).Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase latin letters, digits and single '-' between them");
    }
}

public class CreateBrandDtoValidator : AbstractValidator<CreateBrandDto>
{
    public CreateBrandDtoValidator() =>
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name must not be blank");
}

public class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    private static readonly string[] Directions = ["asc", "desc"];

    public ProductQueryValidator()
    {
        RuleFor(x => x.SortBy).Must(s => ProductSortFields.All.Contains(s.ToLowerInvariant()))
            .WithMessage($"SortBy must be one of: {string.Join(", ", ProductSortFields.All)}");
        RuleFor(x => x.SortDir).Must(s => Directions.Contains(s.ToLowerInvariant()))
            .WithMessage("SortDir must be 'asc' or 'desc'");
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice is not null);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .When(x => x.MinPrice is not null && x.MaxPrice is not null)
            .WithMessage("MaxPrice must be greater than or equal to MinPrice");
    }
}

internal static class PriceRuleExtensions
{
    /// <summary>Ціна: додатна, не більше 2 знаків після коми, у межах decimal(18,2).</summary>
    public static IRuleBuilderOptions<T, decimal> PriceRules<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0)
            .LessThanOrEqualTo(1_000_000_000m)
            .PrecisionScale(18, 2, false).WithMessage("Price may have at most 2 decimal places");
}
