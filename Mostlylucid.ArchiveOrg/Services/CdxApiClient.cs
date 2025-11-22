using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.ArchiveOrg.Config;
using Mostlylucid.ArchiveOrg.Models;

namespace Mostlylucid.ArchiveOrg.Services;

public class CdxApiClient : ICdxApiClient
{
    private const string CdxApiBaseUrl = "https://web.archive.org/cdx/search/cdx";
    private readonly HttpClient _httpClient;
    private readonly ILogger<CdxApiClient> _logger;
    private readonly ArchiveOrgOptions _options;

    public CdxApiClient(
        HttpClient httpClient,
        IOptions<ArchiveOrgOptions> options,
        ILogger<CdxApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<List<CdxRecord>> GetCdxRecordsAsync(
        string url,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var records = new List<CdxRecord>();

        try
        {
            // Build the CDX API URL
            var queryParams = new List<string>
            {
                $"url={Uri.EscapeDataString(url)}/*",
                "output=json",
                "fl=urlkey,timestamp,original,mimetype,statuscode,digest,length"
            };

            // Add date filters if specified
            if (startDate.HasValue)
            {
                queryParams.Add($"from={startDate.Value:yyyyMMdd}");
            }

            if (endDate.HasValue)
            {
                queryParams.Add($"to={endDate.Value:yyyyMMdd}");
            }

            // Add MIME type filter
            if (_options.MimeTypes.Count > 0)
            {
                foreach (var mimeType in _options.MimeTypes)
                {
                    queryParams.Add($"filter=mimetype:{mimeType}");
                }
            }

            // Add status code filter
            if (_options.StatusCodes.Count > 0)
            {
                foreach (var statusCode in _options.StatusCodes)
                {
                    queryParams.Add($"filter=statuscode:{statusCode}");
                }
            }

            // Collapse to unique URLs if requested
            if (_options.UniqueUrlsOnly)
            {
                queryParams.Add("collapse=urlkey");
            }

            var apiUrl = $"{CdxApiBaseUrl}?{string.Join("&", queryParams)}";
            _logger.LogInformation("Fetching CDX records from: {Url}", apiUrl);

            var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("No CDX records found for URL: {Url}", url);
                return records;
            }

            // Parse JSON array response
            var jsonArray = JsonSerializer.Deserialize<string[][]>(content);
            if (jsonArray == null || jsonArray.Length == 0)
            {
                return records;
            }

            // Skip the header row (first row contains column names)
            foreach (var row in jsonArray.Skip(1))
            {
                try
                {
                    var record = CdxRecord.FromJsonArray(row);

                    // Apply include/exclude filters
                    if (ShouldIncludeRecord(record))
                    {
                        records.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse CDX record: {Row}", string.Join(",", row));
                }
            }

            _logger.LogInformation("Found {Count} CDX records for URL: {Url}", records.Count, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching CDX records for URL: {Url}", url);
            throw;
        }

        return records;
    }

    private bool ShouldIncludeRecord(CdxRecord record)
    {
        // Check include patterns
        if (_options.IncludePatterns.Count > 0)
        {
            var matches = _options.IncludePatterns.Any(pattern =>
                System.Text.RegularExpressions.Regex.IsMatch(record.OriginalUrl, pattern));

            if (!matches)
            {
                return false;
            }
        }

        // Check exclude patterns
        if (_options.ExcludePatterns.Count > 0)
        {
            var excluded = _options.ExcludePatterns.Any(pattern =>
                System.Text.RegularExpressions.Regex.IsMatch(record.OriginalUrl, pattern));

            if (excluded)
            {
                return false;
            }
        }

        return true;
    }
}
