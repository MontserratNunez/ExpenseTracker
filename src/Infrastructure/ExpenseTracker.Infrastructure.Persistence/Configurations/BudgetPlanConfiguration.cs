using ExpenseTracker.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public class BudgetPlanConfiguration : IEntityTypeConfiguration<BudgetPlan>
{
    public void Configure(EntityTypeBuilder<BudgetPlan> builder)
    {
        builder.ConfigureOwned();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.SavingsType).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.SavingsValue).HasPrecision(18, 2);
        builder.Property(e => e.AlertThresholdPercent).HasPrecision(5, 2);

        builder.HasOne<IncomeSource>()
            .WithMany()
            .HasForeignKey(e => e.SavingsIncomeSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Allocations)
            .WithOne()
            .HasForeignKey(a => a.BudgetPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.EffectiveFrom });
    }
}
