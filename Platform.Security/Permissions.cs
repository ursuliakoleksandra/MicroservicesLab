namespace Platform.Security;

/// <summary>
/// Дозволи (permissions) платформи за матрицею з Частини 3 (таблиці 6–10). Формат «ресурс:дія».
/// Ресурси: orders-api (Замовлення, Лаб. 02), products-api (Каталог, Лаб. 03), payments-api (заглушка платіжного сервісу).
/// </summary>
public static class Permissions
{
    public static class Orders
    {
        public const string Read = "orders:read";
        public const string Create = "orders:create";
        public const string Update = "orders:update";     // зміна деталей, оплата, відправлення, скасування
        public const string Delete = "orders:delete";     // видалення замовлення
        public const string Approve = "orders:approve";   // підтвердження замовлення (резервування залишку)
    }

    public static class Products
    {
        public const string Read = "products:read";
        public const string Create = "products:create";
        public const string Update = "products:update";   // назва, ціна, залишок, деталі, категорії
        public const string Delete = "products:delete";
        public const string Publish = "products:publish"; // публікація товару (IsActive = true)
    }

    public static class Payments
    {
        public const string Read = "payments:read";
        public const string Process = "payments:process";
        public const string Refund = "payments:refund";
    }
}

/// <summary>
/// Scopes IdentityServer (OAuth 2.0) для M2M-викликів і user-токенів. Назви через крапку; scope «orders.read»
/// відповідає permission «orders:read» (див. PermissionAuthorizationHandler).
/// </summary>
public static class ApiScopes
{
    public const string OrdersRead = "orders.read";
    public const string OrdersCreate = "orders.create";
    public const string OrdersUpdate = "orders.update";
    public const string OrdersDelete = "orders.delete";
    public const string OrdersApprove = "orders.approve";

    public const string ProductsRead = "products.read";
    public const string ProductsCreate = "products.create";
    public const string ProductsUpdate = "products.update";
    public const string ProductsDelete = "products.delete";
    public const string ProductsPublish = "products.publish";

    public const string ReviewsRead = "reviews.read";
    public const string ReviewsWrite = "reviews.write";

    public const string PaymentsRead = "payments.read";
    public const string PaymentsProcess = "payments.process";
    public const string PaymentsRefund = "payments.refund";

    public static readonly string[] All =
    [
        OrdersRead, OrdersCreate, OrdersUpdate, OrdersDelete, OrdersApprove,
        ProductsRead, ProductsCreate, ProductsUpdate, ProductsDelete, ProductsPublish,
        ReviewsRead, ReviewsWrite,
        PaymentsRead, PaymentsProcess, PaymentsRefund
    ];
}

/// <summary>Імена ApiResource (audience) в IdentityServer.</summary>
public static class ApiResourceNames
{
    public const string Orders = "orders_api";
    public const string Products = "products_api";
    public const string Reviews = "reviews_api";
    public const string Payments = "payments_api";
    public const string Aggregator = "aggregator_api";
}
