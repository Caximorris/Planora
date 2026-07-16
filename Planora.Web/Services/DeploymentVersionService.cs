using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planora.Web.Services;

public sealed record DeploymentVersion(string Version, string Commit, DateTimeOffset BuiltAtUtc);

/// <summary>
/// Detects a new deployed web build without ever caching or authenticating the metadata request.
/// A notification is offered instead of an automatic reload so active form edits and drag operations
/// are never interrupted.
/// </summary>
public sealed class DeploymentVersionService : IAsyncDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);
    private readonly HttpClient _client;
    private readonly string? _loadedVersion;
    private readonly CancellationTokenSource _disposeCts = new();
    private Task? _pollingTask;
    private int _started;
    private int _updateAvailable;

    public DeploymentVersionService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _client = httpClientFactory.CreateClient("PlanoraDeploymentVersion");
        _loadedVersion = configuration["BuildVersion"];
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_loadedVersion);
    public bool IsUpdateAvailable => Volatile.Read(ref _updateAvailable) == 1;
    public event Action? Changed;

    public async Task StartAsync()
    {
        if (!IsEnabled || Interlocked.Exchange(ref _started, 1) != 0) return;

        await CheckForUpdateAsync();
        _pollingTask = PollForUpdatesAsync(_disposeCts.Token);
    }

    public async Task CheckForUpdateAsync()
    {
        if (!IsEnabled || IsUpdateAvailable) return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"version.json?cache-bust={Guid.NewGuid():N}");
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };

            using var response = await _client.SendAsync(request, _disposeCts.Token);
            if (!response.IsSuccessStatusCode) return;

            var deployed = await response.Content.ReadFromJsonAsync<DeploymentVersion>(
                cancellationToken: _disposeCts.Token);

            if (string.IsNullOrWhiteSpace(deployed?.Version)
                || string.Equals(deployed.Version, _loadedVersion, StringComparison.Ordinal))
            {
                return;
            }

            if (Interlocked.Exchange(ref _updateAvailable, 1) == 0)
                Changed?.Invoke();
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
            // Component disposal is expected when the browser reloads or closes.
        }
        catch (HttpRequestException)
        {
            // A transient static-host outage must not disrupt the running application.
        }
        catch (JsonException)
        {
            // Invalid deployment metadata is treated as an unavailable update, not an app failure.
        }
    }

    private async Task PollForUpdatesAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await CheckForUpdateAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during app teardown.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();
        if (_pollingTask is not null)
            await _pollingTask;
        _disposeCts.Dispose();
    }
}
