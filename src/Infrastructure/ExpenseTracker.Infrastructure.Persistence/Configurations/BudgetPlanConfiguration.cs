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

        builder.HasMany(e => e.Allocations)
            .WithOne()
            .HasForeignKey(a => a.BudgetPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.EffectiveFrom });
    }
}
