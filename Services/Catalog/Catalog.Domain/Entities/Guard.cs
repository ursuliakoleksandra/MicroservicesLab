using System.Text.RegularExpressions;
using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

internal static class Guard
{
    private static readonly Regex SkuPattern = new("^[A-Z0-9-]+$", RegexOptions.Compiled);
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    public static string NotBlank(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainException($"{field} is required");
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new DomainException($"{field} must be at most {maxLength} characters");
        return trimmed;
    }

    public static string Sku(string? value)
    {
        var sku = NotBlank(value, "SKU", 50);
        if (!SkuPattern.IsMatch(sku)) throw new DomainException("SKU may contain only A-Z, 0-9 and '-'");
        return sku;
    }

    public static string Slug(string? value)
    {
        var slug = NotBlank(value, "Slug", 100);
        if (!SlugPattern.IsMatch(slug)) throw new DomainException("Slug must be lowercase latin letters, digits and '-'");
        return slug;
    }

    public static decimal Price(decimal price)
    {
        if (price <= 0) throw new DomainException("Price must be positive");
        if (price > 1_000_000_000m) throw new DomainException("Price is too large");
        if (decimal.Round(price, 2) != price) throw new DomainException("Price may have at most 2 decimal places");
        return price;
    }
}
