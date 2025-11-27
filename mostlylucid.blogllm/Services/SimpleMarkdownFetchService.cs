using Mostlylucid.Markdig.FetchExtension.Models;
using Mostlylucid.Markdig.FetchExtension.Services;
using System.Collections.Concurrent;

namespace Mostlylucid.BlogLLM.Services;

/// <summary>
/// Simple in-memory implementation of IMarkdownFetchService for the blogllm ingestion tool.
/// Does not persist to database, just fetches and caches in memory for the duration of the run.
/// </summary>
public class SimpleMarkdownFetchService : IMarkdownFetchService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, (string content, DateTimeOffset fetchedAt)> _cache = new();

    public SimpleMarkdownFetchService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<MarkdownFetchResult> FetchMarkdownAsync(string url, int pollFrequencyHours, int blogPostId)
    {
        try
        {
            // Check cache first
            if (_cache.TryGetValue(url, out var cached))
            {
                var age = DateTimeOffset.UtcNow - cached.fetchedAt;
                if (age.TotalHours < pollFrequencyHours)
                {
                    return new MarkdownFetchResult
                    {
                        Success = true,
                        Content = cached.content
                    };
                }
            }

            // Fetch fresh content
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                // Return cached content if available
                if (_cache.TryGetValue(url, out var staleCached))
                {
                    return new MarkdownFetchResult
                    {
                        Success = true,
                        Content = staleCached.content
                    };
                }

                return new MarkdownFetchResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            var content = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(content))
            {
                return new MarkdownFetchResult
                {
                    Success = false,
                    ErrorMessage = "Fetched content was empty"
                };
            }

            // Cache the content
            _cache[url] = (content, DateTimeOffset.UtcNow);

            return new MarkdownFetchResult
            {
                Success = true,
                Content = content
            };
        }
        catch (TaskCanceledException)
        {
            return new MarkdownFetchResult
            {
                Success = false,
                ErrorMessage = "Request timed out after 30 seconds"
            };
        }
        catch (HttpRequestException ex)
        {
            return new MarkdownFetchResult
            {
                Success = false,
                ErrorMessage = $"HTTP request failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new MarkdownFetchResult
            {
                Success = false,
                ErrorMessage = $"Fetch error: {ex.Message}"
            };
        }
    }

    public Task<bool> RemoveCachedMarkdownAsync(string url, int blogPostId = 0)
    {
        var removed = _cache.TryRemove(url, out _);
        return Task.FromResult(removed);
    }
}
