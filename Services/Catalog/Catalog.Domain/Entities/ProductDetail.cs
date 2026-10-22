using System.Text.Json;
using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>1:1 з Product — первинний ключ є водночас зовнішнім.</summary>
public class ProductDetail
{
    public long ProductId { get; private set; }
    public string? Description { get; private set; }
    public int? WeightGrams { get; private set; }
    public int WarrantyMonths { get; private set; } = 12;
    public string? Specs { get; private set; }                       // JSON

    private ProductDetail() { }

    public ProductDetail(string? description, int? weightGrams, int warrantyMonths, string? specsJson)
        => Update(description, weightGrams, warrantyMonths, specsJson);

    public void Update(string? description, int? weightGrams, int warrantyMonths, string? specsJson)
    {
        if (weightGrams is <= 0) throw new DomainException("Weight must be positive");
        if (warrantyMonths < 0) throw new DomainException("Warranty cannot be negative");
        if (description is { Length: > 2000 }) throw new DomainException("Description must be at most 2000 characters");
        if (!string.IsNullOrWhiteSpace(specsJson))
        {
            try { using var _ = JsonDocument.Parse(specsJson); }
            catch (JsonException) { throw new DomainException("Specs must be valid JSON"); }
        }

        Description = description;
        WeightGrams = weightGrams;
        WarrantyMonths = warrantyMonths;
        Specs = string.IsNullOrWhiteSpace(specsJson) ? null : specsJson;
    }
}
