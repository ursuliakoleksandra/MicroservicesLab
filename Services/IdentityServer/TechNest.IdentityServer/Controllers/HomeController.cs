using Microsoft.AspNetCore.Mvc;

namespace TechNest.IdentityServer.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => Content("TechNest IdentityServer. Discovery: /.well-known/openid-configuration");
}
