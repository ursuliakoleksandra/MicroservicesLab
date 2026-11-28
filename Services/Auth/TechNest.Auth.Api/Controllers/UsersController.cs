using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TechNest.Auth.Api.Data;
using TechNest.Auth.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TechNest.Auth.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController(UserManager<ApplicationUser> users, ApplicationDbContext db, ILogger<UsersController> logger) : ControllerBase
{
    /// <summary>Список користувачів (лише Admin).</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var list = await users.Users.OrderBy(u => u.UserName)
            .Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var result = new List<UserDto>();
        foreach (var u in list) result.Add(await ToDtoAsync(u));
        return Ok(result);
    }

    /// <summary>Дані користувача (власник або Admin).</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> Get(string id)
    {
        if (!IsOwnerOrAdmin(id)) return Forbid();
        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(await ToDtoAsync(user));
    }

    /// <summary>Оновлення профілю (власник або Admin).</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> Update(string id, UpdateUserRequest request)
    {
        if (!IsOwnerOrAdmin(id)) return Forbid();
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(await ToDtoAsync(user));
    }

    /// <summary>Видалення користувача (лише Admin); refresh-токени видаляються каскадом.</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);

        logger.LogInformation("User {UserId} deleted by {AdminId}", id, CurrentUserId());
        return NoContent();
    }

    /// <summary>Історія refresh-токенів (власник або Admin). Хеші не повертаються.</summary>
    [HttpGet("{id}/refresh-tokens")]
    public async Task<ActionResult<IEnumerable<RefreshTokenDto>>> GetRefreshTokens(string id, CancellationToken ct)
    {
        if (!IsOwnerOrAdmin(id)) return Forbid();

        var tokens = await db.RefreshTokens.Where(t => t.UserId == id).OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        return Ok(tokens.Select(t => new RefreshTokenDto(t.Id, t.CreatedAt, t.ExpiresAt, t.RevokedAt, t.RevokedReason,
            t.RevokedAt is not null ? "revoked" : t.IsExpired ? "expired" : "active")));
    }

    /// <summary>Ролі користувача (будь-який автентифікований користувач).</summary>
    [HttpGet("{id}/roles")]
    public async Task<ActionResult<IEnumerable<string>>> GetRoles(string id)
    {
        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(await users.GetRolesAsync(user));
    }

    /// <summary>Призначити роль (лише Admin).</summary>
    [HttpPost("{id}/roles")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AddRole(string id, RoleRequest request)
    {
        if (!Roles.All.Contains(request.Role)) return BadRequest($"Unknown role. Allowed: {string.Join(", ", Roles.All)}.");
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var result = await users.AddToRoleAsync(user, request.Role);
        return result.Succeeded ? Ok(await users.GetRolesAsync(user)) : BadRequest(result.Errors);
    }

    /// <summary>Видалити роль (лише Admin).</summary>
    [HttpDelete("{id}/roles/{role}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RemoveRole(string id, string role)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var result = await users.RemoveFromRoleAsync(user, role);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    // Owner-ендпоінти: userId з токена має збігатися з {id} або користувач — Admin
    private bool IsOwnerOrAdmin(string id) => CurrentUserId() == id || User.IsInRole(Roles.Admin);

    private string? CurrentUserId() => User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    private async Task<UserDto> ToDtoAsync(ApplicationUser u) =>
        new(u.Id, u.UserName!, u.Email ?? "", u.EmailConfirmed, u.FullName, u.PhoneNumber, await users.GetRolesAsync(u));
}
