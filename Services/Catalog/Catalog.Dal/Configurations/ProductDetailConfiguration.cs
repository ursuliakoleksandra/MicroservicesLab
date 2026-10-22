using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductDetailConfiguration : IEntityTypeConfiguration<ProductDetail>
{
    public void Configure(EntityTypeBuilder<ProductDetail> builder)
    {
        builder.ToTable("ProductDetails", t =>
        {
            t.HasCheckConstraint("CK_ProductDetails_WeightGrams", "[WeightGrams] > 0");
            t.HasCheckConstraint("CK_ProductDetails_WarrantyMonths", "[WarrantyMonths] >= 0");
            t.HasCheckConstraint("CK_ProductDetails_Specs", "[Specs] IS NULL OR ISJSON([Specs]) = 1");
        });

        builder.HasKey(d => d.ProductId);                                      // PK = FK (1:1)
        builder.Property(d => d.ProductId).ValueGeneratedNever();
        builder.Property(d => d.Description).HasMaxLength(2000);
        // Specs зберігається як nvarchar(max) із перевіркою ISJSON
    }
}
