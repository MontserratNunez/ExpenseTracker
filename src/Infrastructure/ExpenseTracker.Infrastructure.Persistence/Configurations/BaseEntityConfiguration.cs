using ExpenseTracker.Core.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

internal static class BaseEntityConfiguration
{
    /// <summary>Common mapping for every entity owned by a user.</summary>
    public static void ConfigureOwned<T>(this EntityTypeBuilder<T> builder) where T : UserOwnedEntity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(450);
    }
}
