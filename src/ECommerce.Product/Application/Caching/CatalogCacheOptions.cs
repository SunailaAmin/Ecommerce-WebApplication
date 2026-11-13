namespace ECommerce.Products.Application.Caching;

/// <summary>
/// Expiration for the cached catalog reads. Only public, shared data is cached, so a
/// short lifetime keeps a stale read window small even if an invalidation is missed.
/// </summary>
public class CatalogCacheOptions
{
    public const string SectionName = "CatalogCache";

    public int ProductListMinutes { get; set; } = 5;

    public int ProductDetailMinutes { get; set; } = 10;

    public int CategoryMinutes { get; set; } = 30;
}
