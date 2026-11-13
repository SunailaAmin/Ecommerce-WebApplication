namespace ECommerce.Payments.Application.Interfaces;

/// <summary>
/// The payment provider boundary. The application only knows this contract, so the mock
/// used in development can be swapped for a real gateway later without touching the
/// checkout flow.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(decimal amount, string cardNumber, CancellationToken cancellationToken);
}

public sealed record PaymentResult(bool Succeeded, string TransactionId, string? FailureReason)
{
    public static PaymentResult Approved(string transactionId) => new(true, transactionId, null);

    public static PaymentResult Declined(string reason) => new(false, string.Empty, reason);
}
