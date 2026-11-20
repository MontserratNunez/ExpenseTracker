using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExpenseTracker.Web.Services;

/// <summary>
/// Calls into wwwroot/js/ui.js (focus trap, scroll lock, menu keyboard support). The module is loaded the first
/// time it is needed, only in the browser: nothing here runs while the page is being prerendered.
/// </summary>
public sealed class UiInterop : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private Task<IJSObjectReference>? _module;

    public UiInterop(IJSRuntime js) => _js = js;

    private Task<IJSObjectReference> Module => _module ??= _js.InvokeAsync<IJSObjectReference>("import", "./js/ui.js").AsTask();

    /// <summary>Moves the focus into the element and keeps it there. Returns an id for <see cref="ReleaseFocusAsync"/>.</summary>
    public async Task<int> TrapFocusAsync(ElementReference element, DotNetObjectReference<EscapeCallbacks>? escape = null)
        => await (await Module).InvokeAsync<int>("trapFocus", element, escape);

    /// <summary>Stops trapping and returns the focus to the element that had it before.</summary>
    public async Task ReleaseFocusAsync(int trapId)
        => await (await Module).InvokeVoidAsync("releaseFocus", trapId);

    public async Task LockScrollAsync(bool locked)
        => await (await Module).InvokeVoidAsync("lockScroll", locked);

    public async Task<int> AttachMenuAsync(ElementReference root, DotNetObjectReference<DropdownMenuCallbacks> callbacks)
        => await (await Module).InvokeAsync<int>("attachMenu", root, callbacks);

    public async Task DetachMenuAsync(int menuId)
        => await (await Module).InvokeVoidAsync("detachMenu", menuId);

    public async Task FocusMenuItemAsync(ElementReference menu, string which)
        => await (await Module).InvokeVoidAsync("focusMenuItem", menu, which);

    public async Task FocusMenuTriggerAsync(ElementReference root)
        => await (await Module).InvokeVoidAsync("focusMenuTrigger", root);

    public async Task<bool> GetSidebarCollapsedAsync()
        => await (await Module).InvokeAsync<bool>("getSidebarCollapsed");

    public async Task SetSidebarCollapsedAsync(bool collapsed)
        => await (await Module).InvokeVoidAsync("setSidebarCollapsed", collapsed);

    public async Task FocusMainAsync(string id)
        => await (await Module).InvokeVoidAsync("focusMain", id);

    public async Task SubmitFormAsync(string id)
        => await (await Module).InvokeVoidAsync("submitForm", id);

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
            return;

        try
        {
            var module = await _module;
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // the circuit is already gone
        }
    }
}

/// <summary>Lets ui.js tell an overlay that Esc was pressed while the focus was outside it.</summary>
public sealed class EscapeCallbacks
{
    private readonly Func<Task> _onEscape;

    public EscapeCallbacks(Func<Task> onEscape) => _onEscape = onEscape;

    [JSInvokable]
    public Task OnEscape() => _onEscape();
}

/// <summary>What ui.js calls back into when the keyboard acts on a dropdown menu.</summary>
public sealed class DropdownMenuCallbacks
{
    private readonly Func<string, Task> _open;
    private readonly Func<bool, Task> _close;

    public DropdownMenuCallbacks(Func<string, Task> open, Func<bool, Task> close)
    {
        _open = open;
        _close = close;
    }

    [JSInvokable]
    public Task OpenFromKeyboard(string focus) => _open(focus);

    [JSInvokable]
    public Task CloseFromKeyboard(bool restoreFocus) => _close(restoreFocus);
}
