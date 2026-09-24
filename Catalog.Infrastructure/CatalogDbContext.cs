using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Catalog.Infrastructure;

public class CatalogDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductDetails> ProductDetails => Set<ProductDetails>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=localhost,1433;Database=CatalogDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;")
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1. Primary Key для ProductDetails
        modelBuilder.Entity<ProductDetails>()
            .HasKey(d => d.ProductId);

        // 2. Конфігурація 1:1 (Product <-> ProductDetails)
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Details)
            .WithOne(d => d.Product)
            .HasForeignKey<ProductDetails>(d => d.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // 3. Конфігурація 1:N (Category <-> Product)
        modelBuilder.Entity<Category>()
            .HasMany(c => c.Products)
            .WithOne(p => p.Category)
            .HasForeignKey(p => p.CategoryId);

        // 4. Конфігурація M:N (Product <-> Tag)
        modelBuilder.Entity<ProductTag>()
            .HasKey(pt => new { pt.ProductId, pt.TagId });

        // Обмеження та індекси
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Sku)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Product>()
            .Property(p => p.RowVersion)
            .IsRowVersion();

        // Seed Data з фіксованою датою
        var categoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var productId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var staticDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = categoryId, Name = "Ноутбуки" }
        );

        modelBuilder.Entity<Product>().HasData(
            new Product 
            { 
                Id = productId, 
                Name = "Gaming Laptop Pro", 
                Sku = "NB-001", 
                Price = 42000, 
                StockQuantity = 15, 
                CategoryId = categoryId,
                CreatedAt = staticDate
            }
        );

        modelBuilder.Entity<ProductDetails>().HasData(
            new ProductDetails { ProductId = productId, Description = "Флагманський ігровий ноутбук", WeightKg = 2.3 }
        );
    }
}