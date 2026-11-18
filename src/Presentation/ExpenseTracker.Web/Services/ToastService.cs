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
}

/// <summary>
/// Short-lived notifications ("Gasto guardado"). Scoped, so each Blazor circuit has its own list.
/// </summary>
public class ToastService
{
    private const int DefaultDurationMs = 3000;

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

    public void ShowSuccess(string message) => Show(message, ToastKind.Success);

    public void ShowError(string message) => Show(message, ToastKind.Error);

    public void ShowInfo(string message) => Show(message, ToastKind.Info);

    public void Dismiss(Guid id)
    {
        bool removed;
        lock (_lock)
            removed = _items.RemoveAll(t => t.Id == id) > 0;

        if (removed)
            Changed?.Invoke();
    }

    private void Show(string message, ToastKind kind, int durationMs = DefaultDurationMs)
    {
        var toast = new ToastItem { Message = message, Kind = kind };
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
