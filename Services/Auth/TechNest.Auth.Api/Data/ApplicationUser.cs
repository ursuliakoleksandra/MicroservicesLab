using Microsoft.AspNetCore.Identity;

namespace TechNest.Auth.Api.Data;

/// <summary>Користувач платформи: стандартний IdentityUser + ПІБ.</summary>
public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
