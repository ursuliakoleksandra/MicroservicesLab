using Microsoft.AspNetCore.Mvc;
using Platform.Security;
using Platform.Security.Authentication;
using Platform.Security.Authorization;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.AddPlatformAuthentication(ApiResourceNames.Payments);
builder.Services.AddPlatformAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();
app.Run();

/// <summary>Заглушка платіжного сервісу: бізнес-логіки немає — ендпоінти існують, щоб перевірити матрицю payments:* (Keycloak: ресурс payments-api).</summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.Payments.Read)]
    public IActionResult List() => Ok(new[] { new { id = 1, amount = 10.50m, status = "Completed" } });

    [HttpPost("process")]
    [RequirePermission(Permissions.Payments.Process)]
    public IActionResult Process() => Ok(new { id = 2, status = "Processed" });

    [HttpPost("refund")]
    [RequirePermission(Permissions.Payments.Refund)]
    public IActionResult Refund() => Ok(new { id = 1, status = "Refunded" });
}
