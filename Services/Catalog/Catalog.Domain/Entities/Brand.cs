namespace Catalog.Domain.Entities;

public class Brand
{
    public long Id { get; private set; }
    public string Name { get; private set; } = "";
    public string? Country { get; private set; }
    public List<Product> Products { get; } = new();                 // 1:N

    private Brand() { }                                              // для EF Core

    public Brand(string name, string? country = null)
    {
        Name = Guard.NotBlank(name, "Brand name", 100);
        Country = string.IsNullOrWhiteSpace(country) ? null : Guard.NotBlank(country, "Country", 100);
    }
}
