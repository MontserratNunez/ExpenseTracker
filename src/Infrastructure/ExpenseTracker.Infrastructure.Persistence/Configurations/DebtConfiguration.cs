using ExpenseTracker.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public class DebtConfiguration : IEntityTypeConfiguration<Debt>
{
    public void Configure(EntityTypeBuilder<Debt> builder)
    {
        builder.ConfigureOwned();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.DebtType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.OriginalAmount).HasPrecision(18, 2);
        builder.Property(e => e.InitialPaidAmount).HasPrecision(18, 2);
        builder.Property(e => e.MonthlyPayment).HasPrecision(18, 2);
        builder.Property(e => e.AnnualInterestRate).HasPrecision(7, 4);

        builder.HasMany(e => e.Payments)
            .WithOne(p => p.Debt)
            .HasForeignKey(p => p.DebtId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
