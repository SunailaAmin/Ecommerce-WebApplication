using ECommerce.Payments.Application.Interfaces;
using ECommerce.Payments.Domain.Entities;
using ECommerce.Payments.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Payments.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly ECommerceDbContext _context;

    public PaymentRepository(ECommerceDbContext context)
    {
        _context = context;
    }

    public Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return _context.Payments
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Payment?> GetLatestForOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        return _context.Payments
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(Payment payment)
    {
        _context.Payments.Add(payment);
    }
}
