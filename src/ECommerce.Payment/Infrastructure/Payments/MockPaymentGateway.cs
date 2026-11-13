using System.Globalization;
using ECommerce.Payments.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Payments.Infrastructure.Payments;

/// <summary>
/// Development payment provider. It approves every card except the one whose number ends
/// with the configured decline suffix, which is how the declined-payment path is tested.
/// No credentials are involved and no card data is stored or logged.
/// </summary>
public class MockPaymentGateway : IPaymentGateway
{
    private const int TransactionIdLength = 16;

    private readonly PaymentOptions _options;
    private readonly ILogger<MockPaymentGateway> _logger;

    public MockPaymentGateway(IOptions<PaymentOptions> options, ILogger<MockPaymentGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<PaymentResult> ChargeAsync(decimal amount, string cardNumber, CancellationToken cancellationToken)
    {
        if (amount <= 0m)
        {
            return Task.FromResult(PaymentResult.Declined("The payment amount must be greater than zero."));
        }

        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());

        if (!string.IsNullOrEmpty(_options.DeclineCardSuffix)
            && digits.EndsWith(_options.DeclineCardSuffix, StringComparison.Ordinal))
        {
            _logger.LogInformation("Mock payment provider declined a charge of {Amount}", amount);

            return Task.FromResult(PaymentResult.Declined("The card was declined by the payment provider."));
        }

        var transactionId = $"TXN-{Guid.NewGuid():N}"[..TransactionIdLength].ToUpperInvariant();

        _logger.LogInformation(
            "Mock payment provider approved {Amount} as transaction {TransactionId}",
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            transactionId);

        return Task.FromResult(PaymentResult.Approved(transactionId));
    }
}
