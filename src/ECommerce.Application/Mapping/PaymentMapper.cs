using ECommerce.Application.DTOs.Payments;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Mapping;

public static class PaymentMapper
{
    public static PaymentResponse ToResponse(this Payment payment, string orderNumber) => new(
        payment.Id,
        payment.OrderId,
        orderNumber,
        payment.Amount,
        payment.Status,
        payment.TransactionId,
        payment.FailureReason,
        payment.CreatedAt,
        payment.UpdatedAt);
}
