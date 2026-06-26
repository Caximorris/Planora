using System.Net.Http.Json;
using Planora.Shared.DTOs.Notification;

namespace Planora.Web.Services;

public class NotificationService : IAsyncDisposable
{
    private readonly HttpClient _http;
    private CancellationTokenSource _cts = new();

    public int UnreadCount { get; private set; }
    public event Action? OnChange;

    public NotificationService(HttpClient http) => _http = http;

    public void StartPolling() => _ = PollAsync(_cts.Token);

    public async Task StopPollingAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        UnreadCount = 0;
        OnChange?.Invoke();
    }

    private async Task PollAsync(CancellationToken ct)
    {
        await FetchUnreadCountAsync();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(ct);
                await FetchUnreadCountAsync();
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore transient errors */ }
        }
    }

    private async Task FetchUnreadCountAsync()
    {
        try
        {
            var count = await _http.GetFromJsonAsync<int>("api/notifications/unread-count");
            if (count == UnreadCount) return;
            UnreadCount = count;
            OnChange?.Invoke();
        }
        catch { }
    }

    public Task<List<NotificationDto>?> GetRecentAsync(int limit = 15) =>
        _http.GetFromJsonAsync<List<NotificationDto>>($"api/notifications?limit={limit}");

    public async Task<bool> MarkReadAsync(Guid id)
    {
        var ok = (await _http.PutAsync($"api/notifications/{id}/read", null)).IsSuccessStatusCode;
        if (ok) { UnreadCount = Math.Max(0, UnreadCount - 1); OnChange?.Invoke(); }
        return ok;
    }

    public async Task<bool> MarkAllReadAsync()
    {
        var ok = (await _http.PutAsync("api/notifications/read-all", null)).IsSuccessStatusCode;
        if (ok) { UnreadCount = 0; OnChange?.Invoke(); }
        return ok;
    }

    public async Task<bool> DeleteAsync(Guid id, bool wasUnread)
    {
        var ok = (await _http.DeleteAsync($"api/notifications/{id}")).IsSuccessStatusCode;
        if (ok && wasUnread) { UnreadCount = Math.Max(0, UnreadCount - 1); OnChange?.Invoke(); }
        return ok;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
    }
}
