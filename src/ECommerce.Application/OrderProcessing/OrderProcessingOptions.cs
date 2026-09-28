namespace ECommerce.Application.OrderProcessing;

/// <summary>
/// Settings for the periodic order maintenance job.
/// </summary>
public class OrderProcessingOptions
{
    public const string SectionName = "OrderProcessing";

    /// <summary>
    /// How long a Pending order may wait for payment before the cleanup job cancels it.
    /// Zero disables the job, which is the default: cancelling an order stops the customer
    /// retrying a declined card, so the window is a business decision, not a technical one.
    /// </summary>
    public double AbandonedOrderTimeoutMinutes { get; set; }

    /// <summary>
    /// How often the job runs. Doubles rather than whole minutes so a short window can be
    /// checked often enough to be meaningful.
    /// </summary>
    public double CleanupIntervalMinutes { get; set; } = 15;
}
