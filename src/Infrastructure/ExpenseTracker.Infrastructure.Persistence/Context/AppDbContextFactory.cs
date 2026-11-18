using ExpenseTracker.Core.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExpenseTracker.Infrastructure.Persistence.Context;

/// <summary>
/// Used only by the EF tools (migrations). Override the connection with the
/// ConnectionStrings__ExpenseTracker environment variable.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=ExpenseTracker;Trusted_Connection=True;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ExpenseTracker") ?? DefaultConnection;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new AppDbContext(options, new DesignTimeUser());
    }

    private sealed class DesignTimeUser : ICurrentUserService
    {
        public string UserId => string.Empty;
    }
}
