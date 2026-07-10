namespace Planora.Web.Services;

public enum ToastType { Success, Error, Info, Warning }

public sealed class Toast
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Message { get; init; }
    public ToastType Type { get; init; }

    /// <summary>True once dismissal has started; ToastHost renders the exit animation.</summary>
    public bool IsLeaving { get; internal set; }
}

/// <summary>
/// App-wide transient notification service. Components raise toasts via Success/Error/Info/Warning;
/// <see cref="ToastHost"/> subscribes to <see cref="OnChange"/> and renders them. Auto-dismisses
/// after a per-type delay (errors linger longer). Follows the same OnChange + StateHasChanged idiom
/// as NotificationService.
/// </summary>
public sealed class ToastService
{
    private readonly List<Toast> _toasts = [];

    public IReadOnlyList<Toast> Toasts => _toasts;
    public event Action? OnChange;

    public void Success(string message) => Show(message, ToastType.Success);
    public void Error(string message)   => Show(message, ToastType.Error);
    public void Info(string message)    => Show(message, ToastType.Info);
    public void Warning(string message) => Show(message, ToastType.Warning);

    public void Show(string message, ToastType type = ToastType.Info, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var toast = new Toast { Message = message, Type = type };
        _toasts.Add(toast);
        OnChange?.Invoke();

        _ = DismissAfterAsync(toast.Id, duration ?? DefaultDuration(type));
    }

    public void Dismiss(Guid id)
    {
        var toast = _toasts.Find(t => t.Id == id);
        if (toast is null || toast.IsLeaving) return;

        // Two-phase removal: flag it so ToastHost plays the CSS exit animation
        // (.toast-item--leaving, --dur-2 = 180ms), then drop it after the animation ran.
        toast.IsLeaving = true;
        OnChange?.Invoke();
        _ = RemoveAfterExitAsync(id);
    }

    private static TimeSpan DefaultDuration(ToastType type) =>
        type == ToastType.Error ? TimeSpan.FromSeconds(7) : TimeSpan.FromSeconds(4);

    private async Task DismissAfterAsync(Guid id, TimeSpan delay)
    {
        try { await Task.Delay(delay); } catch { /* fire-and-forget */ }
        Dismiss(id);
    }

    private async Task RemoveAfterExitAsync(Guid id)
    {
        try { await Task.Delay(ExitAnimationDuration); } catch { /* fire-and-forget */ }
        if (_toasts.RemoveAll(t => t.Id == id) > 0)
            OnChange?.Invoke();
    }

    /// <summary>Must stay >= the CSS toast-out duration (--dur-2).</summary>
    private static readonly TimeSpan ExitAnimationDuration = TimeSpan.FromMilliseconds(200);
}
