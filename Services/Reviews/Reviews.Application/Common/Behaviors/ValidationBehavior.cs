using FluentValidation;
using MediatR;

namespace Reviews.Application.Common.Behaviors;

/// <summary>
/// Виконує ВСІ валідатори FluentValidation для кожної команди й запиту до виклику обробника.
/// Усі помилки збираються разом; за наявності помилок кидається ValidationException — обробник НЕ викликається.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);          // обробник не викличеться

        return await next();
    }
}
