using ECommerce.Application.DTOs.Orders;

namespace ECommerce.Application.DTOs.Payments;

public sealed record CheckoutResponse(OrderResponse Order, PaymentResponse Payment);
