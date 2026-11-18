namespace ExpenseTracker.Core.Application.Interfaces;

public interface IUserDataSeeder
{
    /// <summary>Creates the settings and default categories for a new user. Safe to call more than once.</summary>
    Task SeedDefaultsForUserAsync(string userId);
}
