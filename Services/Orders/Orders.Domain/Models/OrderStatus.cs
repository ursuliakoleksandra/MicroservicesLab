namespace Orders.Domain.Models;

/// <summary>Статуси замовлення (за зразком OrderStatus в eShop). Значення збігаються з CHECK у БД.</summary>
public static class OrderStatus
{
    public const string Submitted = "Submitted";
    public const string AwaitingValidation = "AwaitingValidation";
    public const string StockConfirmed = "StockConfirmed";
    public const string Paid = "Paid";
    public const string Shipped = "Shipped";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All =
        [Submitted, AwaitingValidation, StockConfirmed, Paid, Shipped, Cancelled];
}

public static class PaymentMethods
{
    public const string Card = "Card";
    public const string Cash = "Cash";
    public const string Transfer = "Transfer";
}
