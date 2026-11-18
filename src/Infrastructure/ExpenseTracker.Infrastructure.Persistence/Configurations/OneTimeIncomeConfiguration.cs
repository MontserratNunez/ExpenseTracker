using ExpenseTracker.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public class OneTimeIncomeConfiguration : IEntityTypeConfiguration<OneTimeIncome>
{
    public void Configure(EntityTypeBuilder<OneTimeIncome> builder)
    {
        builder.ConfigureOwned();
        builder.Property(e => e.Description).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.AmountInBase).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(e => new { e.UserId, e.Date });
    }
}
