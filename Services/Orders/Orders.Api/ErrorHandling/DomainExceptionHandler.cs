using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orders.Domain.Exceptions;

namespace Orders.Api.ErrorHandling;

/// <summary>
/// ЄДИНИЙ глобальний обробник помилок: процедура (THROW) -> DAL (SqlException -> доменний виняток)
/// -> BLL -> тут -> HTTP-статус у форматі ProblemDetails (RFC 9457). try/catch у контролерах не потрібні.
/// </summary>
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        // клієнт розірвав з'єднання — це не помилка сервера
        if (ex is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
        {
            http.Response.StatusCode = 499;
            return true;
        }

        var (status, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            BusinessConflictException => (StatusCodes.Status409Conflict, "Business rule conflict"),   // напр. THROW 50002
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Unhandled exception");
        else
            logger.LogWarning("Request failed with {Status}: {Message}", status, ex.Message);

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // текст доменних винятків безпечний; деталі 500 клієнту не віддаємо
                Detail = status == StatusCodes.Status500InternalServerError ? null : ex.Message
            }
        });
    }
}
