using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Hashing;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// In-memory polling service that tracks URLs (registered by the parser/host),
/// periodically fetches them, and raises ContentUpdated when the XXHash64 changes.
/// </summary>
public class MarkdownFetchUpdateService : BackgroundService, IMarkdownFetchUpdateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MarkdownFetchUpdateService> _logger;
    private readonly FetchPollingOptions _options;

    private class Entry
    {
        public required string Url { get; init; }
        public int PollFrequencyHours { get; set; }
        public string? LastHash { get; set; }
        public string? LastContent { get; set; }
        public DateTimeOffset? LastFetchedAt { get; set; }
        public DateTimeOffset NextDue { get; set; } = DateTimeOffset.MinValue;
        public int ConsecutiveFailures { get; set; }
        public string? LastError { get; set; }
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<MarkdownContentUpdatedEventArgs>? ContentUpdated;

    public MarkdownFetchUpdateService(
        IHttpClientFactory httpClientFactory,
        IOptions<FetchPollingOptions> options,
        ILogger<MarkdownFetchUpdateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public void Register(string url, int pollFrequencyHours)
    {
        var now = DateTimeOffset.UtcNow;
        _entries.AddOrUpdate(url,
            addValueFactory: key => new Entry
            {
                Url = key,
                PollFrequencyHours = Math.Max(1, pollFrequencyHours),
                NextDue = now,
            },
            updateValueFactory: (key, existing) =>
            {
                existing.PollFrequencyHours = Math.Max(1, pollFrequencyHours);
                // Keep next due unchanged if it's sooner, otherwise bring it forward
                if (existing.NextDue > now)
                    existing.NextDue = now;
                return existing;
            });
    }

    public void Unregister(string url)
    {
        _entries.TryRemove(url, out _);
    }

    public bool TryGet(string url, out string? content, out string? hash, out DateTimeOffset? fetchedAt)
    {
        if (_entries.TryGetValue(url, out var entry))
        {
            content = entry.LastContent;
            hash = entry.LastHash;
            fetchedAt = entry.LastFetchedAt;
            return true;
        }
        content = null;
        hash = null;
        fetchedAt = null;
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("MarkdownFetchUpdateService is disabled (FetchPollingOptions.Enabled=false). Polling will not run.");
            // still keep the service alive until cancellation, but do nothing
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            return;
        }

        _logger.LogInformation("MarkdownFetchUpdateService started. Tracked URLs: {Count}", _entries.Count);
        var schedulerDelay = _options.SchedulerTickInterval <= TimeSpan.Zero
            ? TimeSpan.FromMinutes(1)
            : _options.SchedulerTickInterval;

        var semaphore = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentFetches));
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;
                var due = _entries.Values.Where(e => e.NextDue <= now).ToList();

                if (due.Count > 0)
                {
                    _logger.LogDebug("Polling {Count} due URL(s)", due.Count);
                }

                var tasks = new List<Task>();
                foreach (var entry in due)
                {
                    await semaphore.WaitAsync(stoppingToken);
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await PollOnce(entry, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error polling {Url}", entry.Url);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, stoppingToken));
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }

                await Task.Delay(schedulerDelay, stoppingToken);
            }
        }
        finally
        {
            semaphore.Dispose();
            _logger.LogInformation("MarkdownFetchUpdateService stopped");
        }
    }

    private async Task PollOnce(Entry entry, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Mostlylucid.Markdig.FetchExtension");
        client.Timeout = _options.HttpTimeout;

        using var request = new HttpRequestMessage(HttpMethod.Get, entry.Url);
        request.Headers.TryAddWithoutValidation("Accept", "text/plain, text/markdown, */*");

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                entry.ConsecutiveFailures++;
                entry.LastError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                _logger.LogWarning("Fetch failed for {Url}: {Status}", entry.Url, entry.LastError);
                // Schedule next try using frequency to avoid hot looping
                entry.NextDue = DateTimeOffset.UtcNow.AddHours(entry.PollFrequencyHours);
                return;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrEmpty(content))
            {
                entry.ConsecutiveFailures++;
                entry.LastError = "Empty content";
                _logger.LogWarning("Fetch returned empty content for {Url}", entry.Url);
                entry.NextDue = DateTimeOffset.UtcNow.AddHours(entry.PollFrequencyHours);
                return;
            }

            var newHash = ComputeHashHex(content);
            var changed = !string.Equals(newHash, entry.LastHash, StringComparison.Ordinal);

            entry.LastFetchedAt = DateTimeOffset.UtcNow;
            entry.NextDue = entry.LastFetchedAt.Value.AddHours(entry.PollFrequencyHours);
            entry.ConsecutiveFailures = 0;
            entry.LastError = null;

            if (changed)
            {
                entry.LastHash = newHash;
                entry.LastContent = content;

                _logger.LogInformation("Content changed for {Url}. Raising event.", entry.Url);
                RaiseContentUpdated(entry.Url, content, newHash, entry.LastFetchedAt.Value);
            }
        }
        catch (TaskCanceledException)
        {
            entry.ConsecutiveFailures++;
            entry.LastError = "Timeout";
            _logger.LogWarning("Fetch timed out for {Url}", entry.Url);
            entry.NextDue = DateTimeOffset.UtcNow.AddHours(entry.PollFrequencyHours);
        }
        catch (Exception ex)
        {
            entry.ConsecutiveFailures++;
            entry.LastError = ex.Message;
            _logger.LogError(ex, "Error fetching {Url}", entry.Url);
            entry.NextDue = DateTimeOffset.UtcNow.AddHours(entry.PollFrequencyHours);
        }
    }

    private static string ComputeHashHex(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash64 = XxHash64.HashToUInt64(bytes);
        return hash64.ToString("X16");
    }

    private void RaiseContentUpdated(string url, string content, string hash, DateTimeOffset fetchedAt)
    {
        var args = new MarkdownContentUpdatedEventArgs
        {
            Url = url,
            Content = content,
            Hash = hash,
            FetchedAt = fetchedAt
        };
        try
        {
            ContentUpdated?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ContentUpdated event handlers for {Url}", url);
        }
    }
}
