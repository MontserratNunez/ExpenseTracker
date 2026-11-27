using ExpenseTracker.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public class BudgetAllocationConfiguration : IEntityTypeConfiguration<BudgetAllocation>
{
    public void Configure(EntityTypeBuilder<BudgetAllocation> builder)
    {
        builder.ConfigureOwned();
        builder.Property(e => e.AllocationType).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Value).HasPrecision(18, 2);

        builder.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.IncomeSource)
            .WithMany()
            .HasForeignKey(e => e.IncomeSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        // At most one allocation per category in each plan.
        builder.HasIndex(e => new { e.BudgetPlanId, e.CategoryId }).IsUnique();
    }
}
