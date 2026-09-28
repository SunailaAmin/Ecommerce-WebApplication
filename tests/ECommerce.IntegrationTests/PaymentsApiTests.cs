using System.Net;
using System.Net.Http.Json;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.DTOs.Payments;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Data;
using ECommerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.IntegrationTests;

public class PaymentsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PaymentsApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// A customer who has checked out with a card the mock provider declines, leaving a
    /// Pending order that can be retried.
    /// </summary>
    private async Task<(HttpClient Customer, HttpClient Admin, CheckoutResponse Checkout, int AddressId)>
        ArrangeDeclinedAsync()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, price: 75m, quantity: 5);
        var address = await TestData.CreateAddressAsync(customer);

        await TestData.AddCartItemAsync(customer, product.Id, 2);

        var checkout = await TestData.CheckoutAsync(customer, address.Id, TestData.DeclinedCard);

        return (customer, admin, checkout, address.Id);
    }

    [Fact]
    public async Task PayOrder_ConfirmsTheOrder_WhenTheRetrySucceeds()
    {
        var (customer, _, checkout, _) = await ArrangeDeclinedAsync();

        var payment = await TestData.PayOrderAsync(customer, checkout.Order.Id);

        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.Equal(PaymentStatus.Failed, checkout.Payment.Status);
        Assert.False(string.IsNullOrWhiteSpace(payment.TransactionId));

        var order = await customer.GetFromJsonAsync<Application.DTOs.Orders.OrderResponse>(
            $"/api/orders/{checkout.Order.Id}");

        Assert.Equal(OrderStatus.Confirmed, order!.Status);
    }

    [Fact]
    public async Task PayOrder_KeepsTheOrderPending_WhenTheRetryIsAlsoDeclined()
    {
        var (customer, _, checkout, _) = await ArrangeDeclinedAsync();

        var payment = await TestData.PayOrderAsync(customer, checkout.Order.Id, TestData.DeclinedCard);

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal(PaymentStatus.Failed, checkout.Payment.Status);

        var order = await customer.GetFromJsonAsync<Application.DTOs.Orders.OrderResponse>(
            $"/api/orders/{checkout.Order.Id}");

        Assert.Equal(OrderStatus.Pending, order!.Status);
    }

    [Fact]
    public async Task PayOrder_ChargesTheOrderTotal_AndNotAnyClientAmount()
    {
        var (customer, _, checkout, _) = await ArrangeDeclinedAsync();

        // 75 x 2 from the order, whatever the request body claims.
        var payment = await TestData.PayOrderAsync(customer, checkout.Order.Id);

        Assert.Equal(150m, payment.Amount);
        Assert.Equal(150m, checkout.Order.TotalAmount);
    }

    [Fact]
    public async Task PayOrder_ReturnsConflict_WhenTheOrderIsAlreadyPaid()
    {
        var (customer, _, checkout, _) = await ArrangeDeclinedAsync();

        await TestData.PayOrderAsync(customer, checkout.Order.Id);

        var response = await customer.PostAsJsonAsync("/api/payments", new PaymentRequest
        {
            OrderId = checkout.Order.Id,
            CardNumber = TestData.ApprovedCard
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PayOrder_Succeeds_ForAnOrderPlacedWithoutCheckout()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, price: 30m, quantity: 5);
        var address = await TestData.CreateAddressAsync(customer);

        await TestData.AddCartItemAsync(customer, product.Id, 2);

        var order = await TestData.CreateOrderAsync(customer, address.Id);

        // POST /api/orders leaves the order Pending with no payment, so it is still payable.
        Assert.Equal(OrderStatus.Pending, order.Status);

        var payment = await TestData.PayOrderAsync(customer, order.Id);

        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.Equal(order.TotalAmount, payment.Amount);
    }

    [Fact]
    public async Task PayOrder_ReturnsNotFound_ForAnotherCustomersOrder()
    {
        var (customer, _, checkout, _) = await ArrangeDeclinedAsync();
        var other = await _factory.CreateFreshCustomerClientAsync();

        var response = await other.PostAsJsonAsync("/api/payments", new PaymentRequest
        {
            OrderId = checkout.Order.Id,
            CardNumber = TestData.ApprovedCard
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _ = customer;
    }

    [Fact]
    public async Task PayOrder_ReturnsNotFound_WhenTheOrderDoesNotExist()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.PostAsJsonAsync("/api/payments", new PaymentRequest
        {
            OrderId = 999999,
            CardNumber = TestData.ApprovedCard
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PayOrder_ReturnsUnauthorized_WhenSignedOut()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/payments", new PaymentRequest
        {
            OrderId = 1,
            CardNumber = TestData.ApprovedCard
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(0, "4111111111111111")]
    [InlineData(1, "")]
    public async Task PayOrder_ReturnsBadRequest_ForInvalidInput(int orderId, string cardNumber)
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.PostAsJsonAsync("/api/payments", new PaymentRequest
        {
            OrderId = orderId,
            CardNumber = cardNumber
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentById_ReturnsTheCustomersOwnPayment()
    {
        var (customer, _, checkout, _) = await ArrangeDeclinedAsync();

        var payment = await customer.GetFromJsonAsync<PaymentResponse>(
            $"/api/payments/{checkout.Payment.Id}");

        Assert.Equal(checkout.Payment.Id, payment!.Id);
        Assert.Equal(checkout.Order.Id, payment.OrderId);
        Assert.Equal(checkout.Order.OrderNumber, payment.OrderNumber);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("The card was declined by the payment provider.", payment.FailureReason);
    }

    [Fact]
    public async Task GetPaymentById_ReturnsNotFound_ForAnotherCustomersPayment()
    {
        var (_, _, checkout, _) = await ArrangeDeclinedAsync();
        var other = await _factory.CreateFreshCustomerClientAsync();

        var response = await other.GetAsync($"/api/payments/{checkout.Payment.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentById_LetsAnAdminReadAnyPayment()
    {
        var (_, admin, checkout, _) = await ArrangeDeclinedAsync();

        var payment = await admin.GetFromJsonAsync<PaymentResponse>($"/api/payments/{checkout.Payment.Id}");

        Assert.Equal(checkout.Payment.Id, payment!.Id);
    }

    [Fact]
    public async Task GetPaymentById_ReturnsNotFound_WhenThePaymentDoesNotExist()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.GetAsync("/api/payments/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentById_ReturnsUnauthorized_WhenSignedOut()
    {
        var response = await _factory.CreateClient().GetAsync("/api/payments/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RetriedPayments_AreRecordedAsSeparateAttempts()
    {
        var (customer, _, checkout, _) = await ArrangeDeclinedAsync();

        await TestData.PayOrderAsync(customer, checkout.Order.Id, TestData.DeclinedCard);
        var successful = await TestData.PayOrderAsync(customer, checkout.Order.Id);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var payments = await context.Payments
            .AsNoTracking()
            .Where(x => x.OrderId == checkout.Order.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        // The failed attempts stay on record for the order.
        Assert.Equal(3, payments.Count);
        Assert.Equal(PaymentStatus.Failed, payments[0].Status);
        Assert.Equal(PaymentStatus.Failed, payments[1].Status);
        Assert.Equal(PaymentStatus.Successful, payments[2].Status);
        Assert.Equal(successful.Id, payments[2].Id);
    }

    [Fact]
    public async Task GetPaymentForOrder_ReturnsTheLatestAttempt()
    {
        var (customer, _, checkout, _) = await ArrangeDeclinedAsync();

        // A second decline after checkout, so the order holds three attempts by the time
        // the successful one lands. Only the newest is interesting to the storefront.
        await TestData.PayOrderAsync(customer, checkout.Order.Id, TestData.DeclinedCard);
        var successful = await TestData.PayOrderAsync(customer, checkout.Order.Id);

        var payment = await customer.GetFromJsonAsync<PaymentResponse>(
            $"/api/orders/{checkout.Order.Id}/payment");

        Assert.Equal(successful.Id, payment!.Id);
        Assert.Equal(checkout.Order.OrderNumber, payment.OrderNumber);
        Assert.Equal(PaymentStatus.Successful, payment.Status);
    }

    [Fact]
    public async Task GetPaymentForOrder_ReturnsTheFailureReason()
    {
        var (customer, _, checkout, _) = await ArrangeDeclinedAsync();

        var payment = await customer.GetFromJsonAsync<PaymentResponse>(
            $"/api/orders/{checkout.Order.Id}/payment");

        Assert.Equal(PaymentStatus.Failed, payment!.Status);
        Assert.Equal("The card was declined by the payment provider.", payment.FailureReason);
    }

    [Fact]
    public async Task GetPaymentForOrder_ReturnsNotFound_ForAnotherCustomersOrder()
    {
        var (_, _, checkout, _) = await ArrangeDeclinedAsync();
        var other = await _factory.CreateFreshCustomerClientAsync();

        var response = await other.GetAsync($"/api/orders/{checkout.Order.Id}/payment");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentForOrder_ReturnsNotFound_WhenTheOrderHasNoPaymentYet()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var category = await TestData.CreateCategoryAsync(admin);
        var product = await TestData.CreateProductAsync(admin, category.Id, price: 30m, quantity: 5);
        var address = await TestData.CreateAddressAsync(customer);

        await TestData.AddCartItemAsync(customer, product.Id, 2);

        var order = await TestData.CreateOrderAsync(customer, address.Id);

        var response = await customer.GetAsync($"/api/orders/{order.Id}/payment");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentForOrder_ReturnsNotFound_WhenTheOrderDoesNotExist()
    {
        var customer = await _factory.CreateFreshCustomerClientAsync();

        var response = await customer.GetAsync("/api/orders/999999/payment");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentForOrder_ReturnsUnauthorized_WhenSignedOut()
    {
        var response = await _factory.CreateClient().GetAsync("/api/orders/1/payment");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CancellingAnOrder_CancelsTheUnsettledPayment()
    {
        var (customer, admin, checkout, _) = await ArrangeDeclinedAsync();

        var response = await admin.PutAsJsonAsync(
            $"/api/admin/orders/{checkout.Order.Id}/status",
            new UpdateOrderStatusRequest { Status = OrderStatus.Cancelled });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payment = await customer.GetFromJsonAsync<PaymentResponse>(
            $"/api/orders/{checkout.Order.Id}/payment");

        // A dead order should not keep a live-looking payment attached to it.
        Assert.Equal(PaymentStatus.Cancelled, payment!.Status);
        // The reason it never went through is still the interesting part to show.
        Assert.Equal("The card was declined by the payment provider.", payment.FailureReason);
    }

    [Fact]
    public async Task CancellingAnOrder_LeavesASuccessfulPaymentAlone()
    {
        // The money was taken and the mock gateway cannot refund it, so the payment stays
        // Successful rather than being reported as cancelled.
        var (customer, admin, checkout, _) = await ArrangeDeclinedAsync();

        await TestData.PayOrderAsync(customer, checkout.Order.Id);

        var response = await admin.PutAsJsonAsync(
            $"/api/admin/orders/{checkout.Order.Id}/status",
            new UpdateOrderStatusRequest { Status = OrderStatus.Cancelled });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var statuses = await context.Payments
            .AsNoTracking()
            .Where(x => x.OrderId == checkout.Order.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Status)
            .ToListAsync();

        // The declined attempt is unsettled, so it is cancelled with the order. The one
        // that took the money is not.
        Assert.Equal(PaymentStatus.Cancelled, statuses[0]);
        Assert.Equal(PaymentStatus.Successful, statuses[1]);
    }
}
