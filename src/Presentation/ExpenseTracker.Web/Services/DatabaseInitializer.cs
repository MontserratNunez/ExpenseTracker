using ExpenseTracker.Infrastructure.Identity.Context;
using ExpenseTracker.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Web.Services;

public static class DatabaseInitializer
{
    /// <summary>Applies pending migrations for both contexts (development only).</summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        var identity = scope.ServiceProvider.GetRequiredService<IdentityContext>();
        await identity.Database.MigrateAsync();

        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var app = await factory.CreateDbContextAsync();
        await app.Database.MigrateAsync();
    }
}
