using Bunit;
using ExpenseTracker.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Tests.Components;

/// <summary>
/// Base for tests of components that use overlays (modals, panels, menus). Their JavaScript (focus trap,
/// scroll lock) is faked in loose mode: what the JavaScript really does is checked by the browser tests.
/// </summary>
public abstract class UiTestContext : BunitContext
{
    protected UiTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        // An instance (not a registration by type): the test container disposes synchronously and UiInterop is async-disposable.
        Services.AddSingleton(new UiInterop(JSInterop.JSRuntime));
        Services.AddSingleton<ToastService>();
    }
}
