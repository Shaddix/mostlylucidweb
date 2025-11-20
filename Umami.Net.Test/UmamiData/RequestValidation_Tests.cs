using Umami.Net.UmamiData.Models.RequestObjects;

namespace Umami.Net.Test.UmamiData;

/// <summary>
/// Tests for request model validation to ensure data integrity before API calls.
/// Validates date ranges, required fields, and logical constraints.
/// </summary>
public class RequestValidation_Tests
{
    /// <summary>
    /// Verifies that setting StartAtDate after EndAtDate throws ArgumentException.
    /// This prevents invalid date ranges that would cause API errors.
    /// </summary>
    [Fact]
    public void BaseRequest_StartDateAfterEndDate_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            var request = new StatsRequest
            {
                EndAtDate = DateTime.UtcNow.AddDays(-7),
                StartAtDate = DateTime.UtcNow // After end date
            };
        });

        Assert.Contains("must be before EndAtDate", exception.Message);
        Assert.Contains("Suggestion:", exception.Message);
    }

    /// <summary>
    /// Verifies that setting EndAtDate before StartAtDate throws ArgumentException.
    /// </summary>
    [Fact]
    public void BaseRequest_EndDateBeforeStartDate_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            var request = new StatsRequest
            {
                StartAtDate = DateTime.UtcNow,
                EndAtDate = DateTime.UtcNow.AddDays(-7) // Before start date
            };
        });

        Assert.Contains("must be after StartAtDate", exception.Message);
        Assert.Contains("Suggestion:", exception.Message);
    }

    /// <summary>
    /// Verifies that Validate throws InvalidOperationException when StartAtDate is not set.
    /// </summary>
    [Fact]
    public void BaseRequest_MissingStartDate_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new StatsRequest
        {
            EndAtDate = DateTime.UtcNow
            // Missing StartAtDate
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            request.Validate());

        Assert.Contains("StartAtDate is required", exception.Message);
        Assert.Contains("Suggestion:", exception.Message);
        Assert.Contains("DateTime.UtcNow.AddDays(-7)", exception.Message);
    }

    /// <summary>
    /// Verifies that Validate throws InvalidOperationException when EndAtDate is not set.
    /// </summary>
    [Fact]
    public void BaseRequest_MissingEndDate_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new StatsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7)
            // Missing EndAtDate
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            request.Validate());

        Assert.Contains("EndAtDate is required", exception.Message);
        Assert.Contains("Suggestion:", exception.Message);
        Assert.Contains("DateTime.UtcNow", exception.Message);
    }

    /// <summary>
    /// Verifies that Validate throws InvalidOperationException when StartAtDate > EndAtDate.
    /// </summary>
    [Fact]
    public void BaseRequest_ValidateInvalidRange_ThrowsInvalidOperationException()
    {
        // Arrange - Create request with invalid range by manipulating private fields
        var request = new StatsRequest();
        typeof(BaseRequest)
            .GetProperty("StartAtDate")!
            .SetValue(request, DateTime.UtcNow);
        typeof(BaseRequest)
            .GetProperty("EndAtDate")!
            .SetValue(request, DateTime.UtcNow.AddDays(-1));

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            request.Validate());

        Assert.Contains("must be before EndAtDate", exception.Message);
        Assert.Contains("Suggestion:", exception.Message);
    }

    /// <summary>
    /// Verifies that Validate succeeds with valid date range.
    /// </summary>
    [Fact]
    public void BaseRequest_ValidDateRange_DoesNotThrow()
    {
        // Arrange
        var request = new StatsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow
        };

        // Act & Assert
        var exception = Record.Exception(() => request.Validate());
        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that StartAt and EndAt properties return correct Unix milliseconds.
    /// </summary>
    [Fact]
    public void BaseRequest_TimestampConversion_ReturnsCorrectMilliseconds()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc);

        var request = new StatsRequest
        {
            StartAtDate = startDate,
            EndAtDate = endDate
        };

        // Act
        var startTimestamp = request.StartAt;
        var endTimestamp = request.EndAt;

        // Assert
        Assert.Equal(1735689600000, startTimestamp); // 2025-01-01 in Unix milliseconds
        Assert.Equal(1736294400000, endTimestamp);   // 2025-01-08 in Unix milliseconds
        Assert.True(startTimestamp < endTimestamp);
    }

    /// <summary>
    /// Verifies that MetricsRequest validates required Type parameter.
    /// </summary>
    [Fact]
    public void MetricsRequest_ValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var request = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.path,
            Unit = Unit.day
        };

        // Act & Assert
        var exception = Record.Exception(() => request.Validate());
        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that PageViewsRequest validates required Unit parameter.
    /// </summary>
    [Fact]
    public void PageViewsRequest_ValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var request = new PageViewsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Unit = Unit.day
        };

        // Act & Assert
        var exception = Record.Exception(() => request.Validate());
        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that EventsSeriesRequest validates required Unit parameter.
    /// </summary>
    [Fact]
    public void EventsSeriesRequest_ValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var request = new EventsSeriesRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Unit = Unit.hour
        };

        // Act & Assert
        var exception = Record.Exception(() => request.Validate());
        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that optional filters can be set without validation errors.
    /// </summary>
    [Fact]
    public void StatsRequest_WithOptionalFilters_DoesNotThrow()
    {
        // Arrange
        var request = new StatsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Url = "/blog/my-post",
            Referrer = "google.com",
            Country = "US",
            Browser = "Chrome",
            Os = "Windows",
            Device = "desktop",
            Title = "My Post",
            Query = "utm_source=newsletter",
            Host = "example.com",
            Region = "CA",
            City = "San Francisco",
            Event = "button-click"
        };

        // Act & Assert
        var exception = Record.Exception(() => request.Validate());
        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that MetricsRequest limit defaults to 500.
    /// </summary>
    [Fact]
    public void MetricsRequest_DefaultLimit_Is500()
    {
        // Arrange
        var request = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.path
        };

        // Assert
        Assert.Equal(500, request.Limit);
    }

    /// <summary>
    /// Verifies that MetricsRequest can set custom limit.
    /// </summary>
    [Fact]
    public void MetricsRequest_CustomLimit_IsRespected()
    {
        // Arrange
        var request = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.path,
            Limit = 10
        };

        // Assert
        Assert.Equal(10, request.Limit);
    }
}
