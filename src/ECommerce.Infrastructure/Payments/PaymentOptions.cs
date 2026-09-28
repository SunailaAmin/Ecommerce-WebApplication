namespace ECommerce.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>
    /// Test card numbers ending with this value are declined, so the failure path can be
    /// exercised without any real payment credentials.
    /// </summary>
    public string DeclineCardSuffix { get; set; } = "0000";
}
