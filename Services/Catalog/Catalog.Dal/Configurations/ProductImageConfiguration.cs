using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Url).IsRequired().HasMaxLength(500);

        // запит: «зображення товару за порядком»
        builder.HasIndex(i => new { i.ProductId, i.SortOrder });
        // правило: не більше одного головного зображення на товар (унікальний фільтрований індекс)
        builder.HasIndex(i => i.ProductId).IsUnique().HasDatabaseName("UX_ProductImages_Main").HasFilter("[IsMain] = 1");
    }
}
