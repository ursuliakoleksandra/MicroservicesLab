using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Reviews.Domain.Exceptions;

namespace Reviews.WebApi.ErrorHandling;

/// <summary>
/// Глобальний обробник: доменні та MongoDB-винятки -> HTTP-статуси у форматі ProblemDetails (RFC 9457).
/// NotFound -> 404; ValidationException (FluentValidation) і DomainException -> 400;
/// ConflictException (конфлікт версій) і MongoWriteException з DuplicateKey -> 409; інші помилки запису -> 400;
/// MongoConnectionException / TimeoutException (сервер недоступний) -> 503; решта -> 500.
/// Виняток логується один раз — тут.
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
            http.Response.StatusCode = 499;                      // клієнт розірвав з'єднання
            return true;
        }

        var status = MapStatus(ex);
        var problem = CreateProblem(ex, status);

        if (status >= 500)
            logger.LogError(ex, "Request {Method} {Path} failed with {Status}", http.Request.Method, http.Request.Path, status);
        else
            logger.LogWarning("Request {Method} {Path} rejected with {Status}: {ExceptionType}",
                http.Request.Method, http.Request.Path, status, ex.GetType().Name);

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, Exception = ex, ProblemDetails = problem });
    }

    private static int MapStatus(Exception ex) => ex switch
    {
        NotFoundException => StatusCodes.Status404NotFound,
        ValidationException => StatusCodes.Status400BadRequest,
        DomainException => StatusCodes.Status400BadRequest,
        ConflictException => StatusCodes.Status409Conflict,                                   // конфлікт версій
        MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } => StatusCodes.Status409Conflict, // унікальний індекс
        MongoBulkWriteException => StatusCodes.Status400BadRequest,
        MongoWriteException => StatusCodes.Status400BadRequest,
        MongoConnectionException => StatusCodes.Status503ServiceUnavailable,
        TimeoutException => StatusCodes.Status503ServiceUnavailable,                          // server selection timeout
        _ => StatusCodes.Status500InternalServerError
    };

    private ProblemDetails CreateProblem(Exception ex, int status)
    {
        if (ex is ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "request" : e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
            return new ValidationProblemDetails(errors) { Status = status, Title = "Validation failed" };
        }

        var (title, detail) = ex switch
        {
            NotFoundException => ("Resource not found", ex.Message),
            DomainException => ("Business rule violated", ex.Message),
            ConflictException => ("Conflict", ex.Message),
            MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } =>
                ("Conflict", "A document with the same unique key already exists"),
            MongoWriteException => ("Write failed", "The database rejected the write operation"),
            MongoBulkWriteException => ("Write failed", "The database rejected the write operation"),
            MongoConnectionException or TimeoutException => ("Service unavailable", "The database is temporarily unavailable"),
            _ => ("An unexpected error occurred", environment.IsDevelopment() ? ex.ToString() : null)   // деталі 500 — лише в Development
        };

        return new ProblemDetails { Status = status, Title = title, Detail = detail };
    }
}
