using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using TechNest.IdentityServer.Data;
using TechNest.IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace TechNest.IdentityServer.Controllers;

/// <summary>Мінімальна сторінка логіну для Authorization Code flow (офіційний Quickstart UI недоступний як NuGet-пакет).</summary>
public class AccountController(
    SignInManager<ApplicationUser> signIn,
    UserManager<ApplicationUser> users,
    IIdentityServerInteractionService interaction,
    IEventService events) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var context = await interaction.GetAuthorizationContextAsync(model.ReturnUrl);
        if (!ModelState.IsValid) return View(model);

        var result = await signIn.PasswordSignInAsync(model.UserName, model.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = (await users.FindByNameAsync(model.UserName))!;
            await events.RaiseAsync(new Duende.IdentityServer.Events.UserLoginSuccessEvent(user.UserName, user.Id, user.UserName, clientId: context?.Client.ClientId));

            // returnUrl повертає на /connect/authorize, який видасть authorization code
            if (context is not null || Url.IsLocalUrl(model.ReturnUrl)) return Redirect(model.ReturnUrl!);
            return Redirect("~/");
        }

        model.Error = result.IsLockedOut ? "Account is locked." : "Invalid username or password.";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Logout(string? logoutId)
    {
        await signIn.SignOutAsync();
        var context = await interaction.GetLogoutContextAsync(logoutId);
        return string.IsNullOrEmpty(context?.PostLogoutRedirectUri) ? Redirect("~/") : Redirect(context.PostLogoutRedirectUri);
    }
}
