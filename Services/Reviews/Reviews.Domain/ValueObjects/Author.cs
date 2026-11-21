using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.ValueObjects;

/// <summary>Автор відгуку: посилання на покупця з OrdersDb (customerId, без FK) + знімок імені на момент написання.</summary>
public sealed class Author : ValueObject
{
    public long CustomerId { get; }
    public string DisplayName { get; }

    public Author(long customerId, string displayName)
    {
        if (customerId <= 0) throw new DomainException("CustomerId must be positive");
        if (string.IsNullOrWhiteSpace(displayName)) throw new DomainException("DisplayName is required");
        if (displayName.Trim().Length > 100) throw new DomainException("DisplayName must be at most 100 characters");

        CustomerId = customerId;
        DisplayName = displayName.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CustomerId;
        yield return DisplayName;
    }
}
