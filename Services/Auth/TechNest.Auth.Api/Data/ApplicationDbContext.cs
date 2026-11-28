using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TechNest.Auth.Api.Data;

/// <summary>Контекст Identity: AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims… + RefreshTokens.</summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e => e.Property(u => u.FullName).HasMaxLength(200));

        builder.Entity<RefreshToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            e.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);
            e.Property(t => t.RevokedReason).HasMaxLength(100);
            e.Property(t => t.CreatedByIp).HasMaxLength(64);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.UserId);
            e.Ignore(t => t.IsActive);
            e.Ignore(t => t.IsExpired);
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>Для dotnet ef (design-time): рядок підключення тут фіктивний — міграції створюються без доступу до БД.</summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=IdentityDb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
