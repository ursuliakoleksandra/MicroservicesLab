using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

internal static class ValidationExtensions
{
    /// <summary>Явний виклик валідатора FluentValidation; помилки -> ValidationProblemDetails (400) з переліком по полях.</summary>
    public static async Task<ValidationProblemDetails?> ValidateToProblemAsync<T>(
        this IValidator<T> validator, T instance, CancellationToken ct)
    {
        ValidationResult result = await validator.ValidateAsync(instance, ct);
        return result.IsValid ? null : new ValidationProblemDetails(result.ToDictionary());
    }
}
