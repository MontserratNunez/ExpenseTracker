using ExpenseTracker.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ConfigureOwned();
        builder.Property(e => e.BaseCurrency).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.CultureName).IsRequired().HasMaxLength(20);
        builder.HasIndex(e => e.UserId).IsUnique();
    }
}
