namespace Catalog.Domain.Entities;

public class Category
{
    public long Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public List<ProductCategory> ProductCategories { get; } = new(); // M:N через ProductCategory

    private Category() { }

    public Category(string name, string slug)
    {
        Name = Guard.NotBlank(name, "Category name", 100);
        Slug = Guard.Slug(slug);
    }
}
