using System.Diagnostics;
using System.Net;
using Mostlylucid.LlmWebFetcher.Models;

namespace Mostlylucid.LlmWebFetcher.Services;

/// <summary>
/// Fetches web pages with proper HTTP handling.
/// Handles redirects, timeouts, compression, and User-Agent headers.
/// </summary>
public class WebFetcher : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;
    
    public WebFetcher(TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };
        
        _httpClient = new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };
        
        // Set realistic headers - many sites block requests without proper headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", 
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
    }
    
    /// <summary>
    /// Fetches a web page and returns the HTML content with metadata.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>WebPage containing HTML and metadata.</returns>
    public async Task<WebPage> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/html";
            
            stopwatch.Stop();
            
            return new WebPage
            {
                Url = response.RequestMessage?.RequestUri?.ToString() ?? url,
                Html = html,
                StatusCode = (int)response.StatusCode,
                ContentType = contentType,
                FetchTime = stopwatch.Elapsed
            };
        }
        catch (HttpRequestException ex)
        {
            throw new WebFetchException($"HTTP error fetching {url}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new WebFetchException($"Timeout fetching {url}", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Fetch operation was cancelled", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new WebFetchException($"Unexpected error fetching {url}: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Fetches multiple URLs in parallel with rate limiting.
    /// </summary>
    /// <param name="urls">URLs to fetch.</param>
    /// <param name="maxConcurrency">Maximum concurrent requests.</param>
    /// <param name="delayBetweenRequests">Delay between requests to the same domain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of URL to WebPage (or null if failed).</returns>
    public async Task<Dictionary<string, WebPage?>> FetchManyAsync(
        IEnumerable<string> urls, 
        int maxConcurrency = 3,
        TimeSpan? delayBetweenRequests = null,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, WebPage?>();
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var delay = delayBetweenRequests ?? TimeSpan.FromMilliseconds(500);
        
        var tasks = urls.Select(async url =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await Task.Delay(delay, cancellationToken);
                var page = await FetchAsync(url, cancellationToken);
                return (url, page: (WebPage?)page);
            }
            catch (Exception)
            {
                return (url, page: (WebPage?)null);
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        var completed = await Task.WhenAll(tasks);
        
        foreach (var (url, page) in completed)
        {
            results[url] = page;
        }
        
        return results;
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Exception thrown when web fetching fails.
/// </summary>
public class WebFetchException : Exception
{
    public WebFetchException(string message) : base(message) { }
    public WebFetchException(string message, Exception innerException) : base(message, innerException) { }
}
