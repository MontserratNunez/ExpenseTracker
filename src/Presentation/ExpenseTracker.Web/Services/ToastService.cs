namespace ExpenseTracker.Web.Services;

public enum ToastKind
{
    Success = 1,
    Error = 2,
    Info = 3
}

public class ToastItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Message { get; init; } = string.Empty;
    public ToastKind Kind { get; init; }

    /// <summary>Text of the optional button, for example "Deshacer".</summary>
    public string? ActionText { get; init; }

    /// <summary>What the button does. The toast closes first, so it cannot be pressed twice.</summary>
    public Func<Task>? Action { get; init; }
}

/// <summary>
/// Short-lived notifications ("Gasto guardado"). Scoped, so each Blazor circuit has its own list.
/// </summary>
public class ToastService
{
    private const int DefaultDurationMs = 3000;
    private const int WithActionDurationMs = 6000;

    private readonly object _lock = new();
    private readonly List<ToastItem> _items = new();

    /// <summary>Raised when the list changes so the host component can re-render.</summary>
    public event Action? Changed;

    public IReadOnlyList<ToastItem> Items
    {
        get
        {
            lock (_lock)
                return _items.ToList();
        }
    }

    public void ShowSuccess(string message) => Show(message, ToastKind.Success, DefaultDurationMs, null, null);

    public void ShowError(string message) => Show(message, ToastKind.Error, DefaultDurationMs, null, null);

    public void ShowInfo(string message) => Show(message, ToastKind.Info, DefaultDurationMs, null, null);

    /// <summary>A toast with a button (like "Deshacer"). It stays longer so there is time to press it.</summary>
    public void Show(ToastKind kind, string message, string? actionText, Func<Task>? action)
        => Show(message, kind, action is null ? DefaultDurationMs : WithActionDurationMs, actionText, action);

    public void Dismiss(Guid id)
    {
        bool removed;
        lock (_lock)
            removed = _items.RemoveAll(t => t.Id == id) > 0;

        if (removed)
            Changed?.Invoke();
    }

    /// <summary>Closes the toast and runs its action. Does nothing when the toast is already gone.</summary>
    public async Task RunActionAsync(Guid id)
    {
        ToastItem? toast;
        lock (_lock)
        {
            toast = _items.FirstOrDefault(t => t.Id == id);
            if (toast is not null)
                _items.Remove(toast);   // taken out under the lock: a second press finds nothing
        }

        if (toast is null)
            return;

        Changed?.Invoke();
        if (toast.Action is not null)
            await toast.Action();
    }

    private void Show(string message, ToastKind kind, int durationMs, string? actionText, Func<Task>? action)
    {
        var toast = new ToastItem { Message = message, Kind = kind, ActionText = actionText, Action = action };
        lock (_lock)
            _items.Add(toast);
        Changed?.Invoke();

        // Fire and forget: the toast removes itself after a few seconds.
        _ = RemoveLaterAsync(toast.Id, durationMs);
    }

    private async Task RemoveLaterAsync(Guid id, int durationMs)
    {
        await Task.Delay(durationMs);
        Dismiss(id);
    }
}
