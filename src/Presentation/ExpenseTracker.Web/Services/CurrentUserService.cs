using System.Security.Claims;
using ExpenseTracker.Core.Application.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;

namespace ExpenseTracker.Web.Services;

/// <summary>
/// Gets the user id from the authentication state of the current circuit (or request).
/// IHttpContextAccessor is not used because HttpContext is not reliable inside a Blazor Server circuit.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public CurrentUserService(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public string UserId
    {
        get
        {
            // The state is already resolved for the circuit, so this task is normally complete.
            var state = _authenticationStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult();
            return state.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }
    }
}
