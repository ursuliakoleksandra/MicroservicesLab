using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.ValueObjects;

/// <summary>Підтвердження покупки: посилання на замовлення з OrdersDb (orderId, без FK).</summary>
public sealed class VerifiedPurchase : ValueObject
{
    public long OrderId { get; }

    public VerifiedPurchase(long orderId)
    {
        if (orderId <= 0) throw new DomainException("OrderId must be positive");
        OrderId = orderId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return OrderId;
    }
}
