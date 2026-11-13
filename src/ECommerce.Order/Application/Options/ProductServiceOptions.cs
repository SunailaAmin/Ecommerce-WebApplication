namespace ECommerce.Orders.Application.Options;

/// <summary>
/// How Order reaches Product Service. Both values come from configuration so a deployment
/// can repoint the client without touching code.
/// </summary>
public class ProductServiceOptions
{
    public const string SectionName = "Services:Product";

    /// <summary>
    /// Base address of the Product Service, including the scheme and port.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Value sent in the X-Service-Key header. Held in configuration, never in source.
    /// </summary>
    public string ServiceKey { get; set; } = string.Empty;
}
