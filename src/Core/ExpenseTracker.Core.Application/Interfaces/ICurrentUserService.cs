namespace ExpenseTracker.Core.Application.Interfaces;

public interface ICurrentUserService
{
    /// <summary>Id of the authenticated user, or an empty string when there is none.</summary>
    string UserId { get; }
}
