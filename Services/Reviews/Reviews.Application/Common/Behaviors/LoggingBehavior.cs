using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Common.Behaviors;

/// <summary>Структуроване логування кожного запиту й відповіді; зовнішній behavior у pipeline.</summary>
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName} {@Request}", name, request);

        try
        {
            var response = await next();
            logger.LogInformation("Handled {RequestName} successfully", name);
            return response;
        }
        catch (ValidationException ex)
        {
            // у лозі видно, що валідація відхилила запит і обробник НЕ виконувався
            logger.LogWarning("Request {RequestName} rejected by validation ({ErrorCount} errors); handler was NOT invoked",
                name, ex.Errors.Count());
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Request {RequestName} failed with {ExceptionType}: {Reason}", name, ex.GetType().Name, ex.Message);
            throw;
        }
    }
}
