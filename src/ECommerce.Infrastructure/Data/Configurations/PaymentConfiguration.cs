using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        builder.Property(x => x.TransactionId)
            .HasMaxLength(100);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(300);

        builder.HasIndex(x => x.OrderId);
    }
}
