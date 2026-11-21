using MediatR;
using Microsoft.AspNetCore.Mvc;
using Reviews.Domain.Exceptions;

namespace Reviews.WebApi.Controllers;

/// <summary>Базовий контролер: доступ до ISender (MediatR). Контролери лише передають команди й запити — без бізнес-логіки й без доступу до БД.</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>Очікувана версія документа із заголовка If-Match (наприклад, "3" або W/"3"); null, якщо заголовка немає.</summary>
    protected int? ExpectedVersion
    {
        get
        {
            var header = Request.Headers.IfMatch.ToString();
            if (string.IsNullOrWhiteSpace(header)) return null;

            var value = header.Trim();
            if (value.StartsWith("W/", StringComparison.Ordinal)) value = value[2..];
            value = value.Trim('"', ' ');
            return int.TryParse(value, out var version) && version >= 0
                ? version
                : throw new DomainException("If-Match header must contain the document version, e.g. \"3\"");
        }
    }

    /// <summary>ETag = версія документа (optimistic concurrency).</summary>
    protected void SetETag(int version) => Response.Headers.ETag = $"\"{version}\"";
}
