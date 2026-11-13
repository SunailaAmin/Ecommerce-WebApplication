using ECommerce.Shared.Enums;

namespace ECommerce.Shared.DTOs;

public sealed record PaymentResponse(
    int Id,
    int OrderId,
    string OrderNumber,
    decimal Amount,
    PaymentStatus Status,
    string? TransactionId,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);
