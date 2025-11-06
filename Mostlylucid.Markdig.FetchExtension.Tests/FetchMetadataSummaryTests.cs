using Mostlylucid.Markdig.FetchExtension.Models;
using Mostlylucid.Markdig.FetchExtension.Utilities;

namespace Mostlylucid.Markdig.FetchExtension.Tests;

public class FetchMetadataSummaryTests
{
    [Fact]
    public void Format_WithDefaultTemplate_ReturnsFormattedSummary()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow.AddHours(-2),
            IsCached = true,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };

        // Act
        var summary = FetchMetadataSummary.Format(result);

        // Assert
        Assert.NotEmpty(summary);
        Assert.Contains("example.com", summary);
        Assert.Contains("2 hour", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_WithCustomTemplate_ReturnsCustomFormatted()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow.AddMinutes(-30),
            IsCached = true,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 12
        };
        var template = "Status: {status} | Age: {age}";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Equal("<div class=\"ft_summary\">Status: cached | Age: 30 minutes ago</div>", summary);
    }

    [Fact]
    public void Format_RetrievedWithShortFormat_ReturnsShortDate()
    {
        // Arrange
        var date = new DateTime(2024, 11, 15, 14, 30, 0, DateTimeKind.Utc);
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = date,
            IsCached = false,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };
        var template = "{retrieved:short}";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Equal("<div class=\"ft_summary\">15 Nov 2024</div>", summary);
    }

    [Fact]
    public void Format_RetrievedWithLongFormat_ReturnsLongDate()
    {
        // Arrange
        var date = new DateTime(2024, 11, 15, 14, 30, 0, DateTimeKind.Utc);
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = date,
            IsCached = false,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };
        var template = "{retrieved:long}";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Equal("<div class=\"ft_summary\">15 November 2024 14:30</div>", summary);
    }

    [Fact]
    public void Format_RetrievedWithCustomFormat_ReturnsCustomDate()
    {
        // Arrange
        var date = new DateTime(2024, 11, 15, 14, 30, 0, DateTimeKind.Utc);
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = date,
            IsCached = false,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };
        var template = "{retrieved:yyyy-MM-dd}";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Equal("<div class=\"ft_summary\">2024-11-15</div>", summary);
    }

    [Fact]
    public void Format_Age_ReturnsRelativeTime()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow.AddHours(-3),
            IsCached = true,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };
        var template = "{age}";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Contains("3 hour", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ago", summary);
    }

    [Fact]
    public void Format_Status_ReturnsCacheStatus()
    {
        // Arrange - Fresh content
        var freshResult = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow,
            IsCached = false,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };

        // Act
        var freshSummary = FetchMetadataSummary.Format(freshResult, "{status}");

        // Assert
        Assert.Equal("<div class=\"ft_summary\">fresh</div>", freshSummary);

        // Arrange - Cached content
        var cachedResult = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow,
            IsCached = true,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };
        var cachedSummary = FetchMetadataSummary.Format(cachedResult, "{status}");
        Assert.Equal("<div class=\"ft_summary\">cached</div>", cachedSummary);

        // Arrange - Stale content
        var staleResult = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow,
            IsCached = true,
            IsStale = true,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };
        var staleSummary = FetchMetadataSummary.Format(staleResult, "{status}");
        Assert.Equal("<div class=\"ft_summary\">stale</div>", staleSummary);
    }

    [Fact]
    public void Format_NextRefresh_ReturnsRelativeTime()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow.AddHours(-2),
            IsCached = true,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };
        var template = "{nextrefresh:relative}";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Contains("hour", summary, StringComparison.OrdinalIgnoreCase); // Could be 21-22 hours depending on exact timing
    }

    [Fact]
    public void Format_NextRefreshWithZeroPoll_ReturnsAlwaysFresh()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow,
            IsCached = false,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 0
        };
        var template = "{nextrefresh}";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Equal("<div class=\"ft_summary\">always fresh</div>", summary);
    }

    [Fact]
    public void Format_Url_ReturnsSourceUrl()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow,
            IsCached = false,
            IsStale = false,
            SourceUrl = "https://example.com/docs.md",
            PollFrequencyHours = 24
        };
        var template = "{url}";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Equal("<div class=\"ft_summary\">https://example.com/docs.md</div>", summary);
    }

    [Fact]
    public void Format_PollFrequency_ReturnsHours()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow,
            IsCached = false,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 12
        };
        var template = "Refreshes every {pollfrequency} hours";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Equal("<div class=\"ft_summary\">Refreshes every 12 hours</div>", summary);
    }

    [Fact]
    public void Format_MultiplePlaceholders_ReplacesAll()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow.AddHours(-1),
            IsCached = true,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };
        var template = "Status: {status} | Updated {age} | Poll: {pollfrequency}h";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Contains("Status: cached", summary);
        Assert.Contains("Updated 1 hour ago", summary);
        Assert.Contains("Poll: 24h", summary);
    }

    [Fact]
    public void Format_UnknownPlaceholder_KeepsOriginal()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = DateTime.UtcNow,
            IsCached = false,
            IsStale = false,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };
        var template = "Status: {unknown_placeholder}";

        // Act
        var summary = FetchMetadataSummary.Format(result, template);

        // Assert
        Assert.Equal("<div class=\"ft_summary\">Status: {unknown_placeholder}</div>", summary);
    }

    [Fact]
    public void Format_FailedResult_ReturnsEmpty()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = false,
            ErrorMessage = "Failed to fetch"
        };

        // Act
        var summary = FetchMetadataSummary.Format(result);

        // Assert
        Assert.Empty(summary);
    }

    [Fact]
    public void Format_NoLastRetrieved_ReturnsEmpty()
    {
        // Arrange
        var result = new MarkdownFetchResult
        {
            Success = true,
            Content = "# Test",
            LastRetrieved = null,
            SourceUrl = "https://example.com/test.md",
            PollFrequencyHours = 24
        };

        // Act
        var summary = FetchMetadataSummary.Format(result);

        // Assert
        Assert.Empty(summary);
    }
}
