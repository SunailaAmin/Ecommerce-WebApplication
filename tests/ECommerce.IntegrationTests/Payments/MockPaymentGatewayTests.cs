using ECommerce.Application.Interfaces;
using ECommerce.Infrastructure.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.IntegrationTests.Payments;

public class MockPaymentGatewayTests
{
    private static MockPaymentGateway CreateGateway(string declineCardSuffix = "0000") =>
        new(Options.Create(new PaymentOptions { DeclineCardSuffix = declineCardSuffix }),
            NullLogger<MockPaymentGateway>.Instance);

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("5555555555554444")]
    [InlineData("1234")]
    public async Task ChargeAsync_ApprovesAnOrdinaryCard(string cardNumber)
    {
        var result = await CreateGateway().ChargeAsync(100m, cardNumber, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.FailureReason);
        Assert.False(string.IsNullOrWhiteSpace(result.TransactionId));
    }

    [Theory]
    [InlineData("4111111111110000")]
    [InlineData("411111111111 0000")]
    public async Task ChargeAsync_DeclinesTheConfiguredTestCard(string cardNumber)
    {
        var result = await CreateGateway().ChargeAsync(100m, cardNumber, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("The card was declined by the payment provider.", result.FailureReason);
        Assert.Equal(string.Empty, result.TransactionId);
    }

    [Fact]
    public async Task ChargeAsync_IgnoresSeparatorsWhenMatchingTheDeclineSuffix()
    {
        var result = await CreateGateway().ChargeAsync(100m, "4111-1111-1111-0000", CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ChargeAsync_ApprovesEverything_WhenNoDeclineSuffixIsConfigured()
    {
        var result = await CreateGateway(declineCardSuffix: string.Empty)
            .ChargeAsync(100m, "4111111111110000", CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    public async Task ChargeAsync_DeclinesANonPositiveAmount(decimal amount)
    {
        var result = await CreateGateway().ChargeAsync(amount, "4111111111111111", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("The payment amount must be greater than zero.", result.FailureReason);
    }

    [Fact]
    public async Task ChargeAsync_ReturnsADistinctTransactionIdPerCharge()
    {
        var gateway = CreateGateway();

        var first = await gateway.ChargeAsync(10m, "4111111111111111", CancellationToken.None);
        var second = await gateway.ChargeAsync(10m, "4111111111111111", CancellationToken.None);

        Assert.NotEqual(first.TransactionId, second.TransactionId);
    }

    [Fact]
    public async Task ChargeAsync_ReturnsATransactionIdThatFitsTheColumn()
    {
        var result = await CreateGateway().ChargeAsync(10m, "4111111111111111", CancellationToken.None);

        // TransactionId is mapped to a 100 character column.
        Assert.InRange(result.TransactionId.Length, 1, 100);
        Assert.StartsWith("TXN-", result.TransactionId);
    }
}
