using Microsoft.AspNetCore.Identity;

namespace TechNest.IdentityServer.Data;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
}
