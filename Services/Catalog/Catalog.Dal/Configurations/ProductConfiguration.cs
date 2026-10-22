using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", t =>
        {
            t.HasCheckConstraint("CK_Products_Price", "[Price] > 0");
            t.HasCheckConstraint("CK_Products_AvailableStock", "[AvailableStock] >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(p => p.RowVersion).IsRowVersion();                  // оптимістична конкурентність

        builder.HasIndex(p => p.Sku).IsUnique();                              // унікальний SKU
        builder.HasIndex(p => p.Name);                                        // пошук/сортування за назвою

        builder.HasOne(p => p.Brand)                                          // N:1 Brand -> Products
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);                               // бренд з товарами не каскадимо

        builder.HasOne(p => p.Detail)                                         // 1:1 (FK є також PK)
            .WithOne()
            .HasForeignKey<ProductDetail>(d => d.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Images)                                        // 1:N
            .WithOne()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
