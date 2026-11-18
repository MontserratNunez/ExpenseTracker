using ExpenseTracker.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public class DebtPaymentConfiguration : IEntityTypeConfiguration<DebtPayment>
{
    public void Configure(EntityTypeBuilder<DebtPayment> builder)
    {
        builder.ConfigureOwned();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.InterestPortion).HasPrecision(18, 2);
        builder.Property(e => e.PrincipalPortion).HasPrecision(18, 2);
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.HasIndex(e => new { e.UserId, e.Date });
    }
}
