using ECommerce.Orders.Application.DTOs.Orders;
using ECommerce.Shared.DTOs;

namespace ECommerce.Orders.Application.DTOs.Payments;

public sealed record CheckoutResponse(OrderResponse Order, PaymentResponse Payment);
