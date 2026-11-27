using ExpenseTracker.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public class IncomeSourceRevisionConfiguration : IEntityTypeConfiguration<IncomeSourceRevision>
{
    public void Configure(EntityTypeBuilder<IncomeSourceRevision> builder)
    {
        builder.ConfigureOwned();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Frequency).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Note).HasMaxLength(200);
        builder.HasIndex(e => new { e.IncomeSourceId, e.EffectiveFrom }).IsUnique();
    }
}
