using System.Security.Cryptography;
using System.Text;
using Catalog.Bll.Dtos;
using Platform.Caching;

namespace Catalog.Bll.Caching;

/// <summary>Схема імен ключів кешу Каталогу: catalog:{сутність}:{ідентифікатор}. Префікс ізолює ключі від інших сервісів у спільному Redis.</summary>
public static class CatalogCacheKeys
{
    public const string ProductsScope = "catalog:products:list";

    public static string Product(long id) => $"catalog:product:{id}";
    public const string Categories = "catalog:categories:all";
    public const string Brands = "catalog:brands:all";

    /// <summary>Ключ сторінки списку: версія групи + хеш нормалізованих параметрів запиту (довільний search не потрапляє в ключ «як є»).</summary>
    public static string ProductList(string version, string signature) =>
        $"{ProductsScope}:v{version}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)))[..16].ToLowerInvariant()}";

    public static string Signature(ProductQuery q, int page, int pageSize) =>
        string.Join('|', page, pageSize, q.Search?.Trim().ToLowerInvariant(), q.MinPrice, q.MaxPrice, q.CategoryId, q.BrandId,
            q.InStock, q.SortBy.ToLowerInvariant(), q.SortDir.ToLowerInvariant());
}

/// <summary>
/// TTL за типом даних. L1 завжди коротший за L2; чим рідше змінюються дані — тим довший TTL.
/// Інвалідація при записі діє незалежно від TTL, TTL лише обмежує «життя» записів, яких ніхто не інвалідував.
/// </summary>
public static class CatalogCachePolicies
{
    /// <summary>Картка товару: читають часто (sliding), змінюється помірно.</summary>
    public static readonly CachePolicy Product = new()
    {
        L1Sliding = TimeSpan.FromSeconds(30), L1Absolute = TimeSpan.FromMinutes(1), L2Absolute = TimeSpan.FromMinutes(10)
    };

    /// <summary>Довідники (категорії, бренди): змінюються рідко — довгий absolute TTL.</summary>
    public static readonly CachePolicy Reference = new()
    {
        L1Absolute = TimeSpan.FromMinutes(5), L2Absolute = TimeSpan.FromHours(1)
    };

    /// <summary>Сторінки списку: багато варіантів ключів і залежність від залишків — короткий TTL.</summary>
    public static readonly CachePolicy List = new()
    {
        L1Absolute = TimeSpan.FromSeconds(30), L2Absolute = TimeSpan.FromMinutes(2)
    };
}
