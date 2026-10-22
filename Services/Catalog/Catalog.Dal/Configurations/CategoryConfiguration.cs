using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(100);
        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasIndex(c => c.Slug).IsUnique();

        // статичний сідінг довідника (фіксовані Id)
        builder.HasData(
            new { Id = 1L, Name = "Ноутбуки", Slug = "laptops" },
            new { Id = 2L, Name = "Смартфони", Slug = "smartphones" },
            new { Id = 3L, Name = "Аудіо", Slug = "audio" },
            new { Id = 4L, Name = "Аксесуари", Slug = "accessories" },
            new { Id = 5L, Name = "Монітори", Slug = "monitors" },
            new { Id = 6L, Name = "Для геймерів", Slug = "gaming" });
    }
}
