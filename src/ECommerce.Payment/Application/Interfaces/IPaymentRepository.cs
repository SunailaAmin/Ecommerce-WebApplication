using ECommerce.Payments.Domain.Entities;

namespace ECommerce.Payments.Application.Interfaces;

public interface IPaymentRepository
{
    /// <summary>
    /// Returns the payment with its order loaded. Tracked for update.
    /// </summary>
    Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// The most recent payment attempt on an order, or null when it has not been paid
    /// for yet. An order can have several attempts after a decline.
    /// </summary>
    Task<Payment?> GetLatestForOrderAsync(int orderId, CancellationToken cancellationToken);

    void Add(Payment payment);
}
