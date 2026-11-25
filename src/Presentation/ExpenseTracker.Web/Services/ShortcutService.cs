using Microsoft.JSInterop;

namespace ExpenseTracker.Web.Services;

/// <summary>What a page gets when a keyboard shortcut is pressed. Set <see cref="Handled"/> to stop the default action.</summary>
public sealed class ShortcutEventArgs
{
    public ShortcutEventArgs(string key) => Key = key;

    /// <summary>"n", "ArrowLeft", "ArrowRight", "/", "?", "Escape" or "mod+k" (Ctrl or Cmd with K).</summary>
    public string Key { get; }

    public bool Handled { get; set; }
}

/// <summary>
/// Keyboard shortcuts of the app. wwwroot/js/shortcuts.js reports each key; the pages that care (the month arrows,
/// the filter) subscribe here, and what no page handles falls to the global default (new expense, command palette, help).
/// </summary>
public sealed class ShortcutService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly List<Func<ShortcutEventArgs, Task>> _handlers = new();
    private Task<IJSObjectReference>? _module;

    public ShortcutService(IJSRuntime js) => _js = js;

    private Task<IJSObjectReference> Module => _module ??= _js.InvokeAsync<IJSObjectReference>("import", "./js/shortcuts.js").AsTask();

    /// <summary>Listens to the shortcuts until the returned object is disposed (do it when the page goes away).</summary>
    public IDisposable Subscribe(Func<ShortcutEventArgs, Task> handler)
    {
        _handlers.Add(handler);
        return new Subscription(() => _handlers.Remove(handler));
    }

    /// <summary>Tells every subscriber about the key. Returns true when one of them handled it.</summary>
    public async Task<bool> RaiseAsync(string key)
    {
        var args = new ShortcutEventArgs(key);
        foreach (var handler in _handlers.ToArray())
        {
            await handler(args);
            if (args.Handled)
                break;
        }

        return args.Handled;
    }

    /// <summary>Starts listening to the keyboard in the browser. Returns what to dispose to stop.</summary>
    public async Task<IJSObjectReference> RegisterAsync(DotNetObjectReference<ShortcutCallbacks> callbacks)
        => await (await Module).InvokeAsync<IJSObjectReference>("register", callbacks);

    /// <summary>Focuses what the page marked with data-shortcut-focus="name". False when the page has nothing to focus.</summary>
    public async Task<bool> FocusTargetAsync(string name)
        => await (await Module).InvokeAsync<bool>("focusTarget", name);

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
            return;

        try
        {
            await (await _module).DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // the circuit is gone
        }
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _dispose;

        public Subscription(Action dispose) => _dispose = dispose;

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}

/// <summary>The object the browser calls when a shortcut is pressed.</summary>
public sealed class ShortcutCallbacks
{
    private readonly Func<string, Task> _onShortcut;

    public ShortcutCallbacks(Func<string, Task> onShortcut) => _onShortcut = onShortcut;

    [JSInvokable]
    public Task OnShortcut(string key) => _onShortcut(key);
}
