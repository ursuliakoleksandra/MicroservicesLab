using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Name).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Country).HasMaxLength(100);
        builder.HasIndex(b => b.Name).IsUnique();

        // статичний сідінг довідника (фіксовані Id) — потрапляє в міграцію
        builder.HasData(
            new { Id = 1L, Name = "Nova", Country = "USA" },
            new { Id = 2L, Name = "Aurix", Country = "South Korea" },
            new { Id = 3L, Name = "Sonic", Country = "Japan" },
            new { Id = 4L, Name = "Gridline", Country = "Germany" },
            new { Id = 5L, Name = "Lumen", Country = "Taiwan" });
    }
}
