using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.ErrorHandling;

/// <summary>
/// Глобальна обробка помилок: виняток -> HTTP-статус + ProblemDetails (RFC 9457).
/// EntityNotFound -> 404, Validation -> 400, Conflict/Domain -> 409, інше -> 500.
/// Виняток логується ОДИН раз — тут. Деталі 500 віддаються лише в Development.
/// </summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment environment,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        if (ex is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
        {
            http.Response.StatusCode = 499;                       // клієнт розірвав з'єднання — не помилка сервера
            return true;
        }

        var (status, title) = ex switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status409Conflict, "Business rule violated"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status >= 500)
            logger.LogError(ex, "Request {Method} {Path} failed", http.Request.Method, http.Request.Path);
        else
            logger.LogWarning("Request {Method} {Path} rejected with {Status}: {Reason}",
                http.Request.Method, http.Request.Path, status, ex.Message);

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status < 500 ? ex.Message : environment.IsDevelopment() ? ex.ToString() : null
            }
        });
    }
}
