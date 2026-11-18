using ExpenseTracker.Infrastructure.Identity.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Identity.Context;

public class IdentityContext : IdentityDbContext<ApplicationUser>
{
    public const string Schema = "identity";

    public IdentityContext(DbContextOptions<IdentityContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().Property(u => u.FullName).HasMaxLength(100);
    }
}
