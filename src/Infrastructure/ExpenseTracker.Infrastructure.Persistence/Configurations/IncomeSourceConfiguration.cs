using ExpenseTracker.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public class IncomeSourceConfiguration : IEntityTypeConfiguration<IncomeSource>
{
    public void Configure(EntityTypeBuilder<IncomeSource> builder)
    {
        builder.ConfigureOwned();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => new { e.UserId, e.StartDate });

        builder.HasMany(e => e.Revisions)
            .WithOne()
            .HasForeignKey(r => r.IncomeSourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
