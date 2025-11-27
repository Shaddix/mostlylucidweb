using Microsoft.Extensions.DependencyInjection;
using Umami.Net.UmamiData;
using Umami.Net.UmamiData.Models;
using Umami.Net.UmamiData.Models.RequestObjects;

namespace Umami.Net.Test.UmamiData;

/// <summary>
/// Tests for Umami version detection and compatibility
/// </summary>
public class UmamiData_Version_Test : UmamiDataBase
{
    [Fact]
    public void UmamiVersionInfo_DefaultState()
    {
        // Arrange & Act
        var versionInfo = new UmamiVersionInfo();

        // Assert
        Assert.Equal(UmamiApiVersion.Unknown, versionInfo.ApiVersion);
        Assert.Null(versionInfo.ServerVersion);
        Assert.Null(versionInfo.DetectedAt);
        Assert.False(versionInfo.IsDetected);
    }

    [Fact]
    public void UmamiVersionInfo_V1_Detection()
    {
        // Arrange & Act
        var versionInfo = new UmamiVersionInfo
        {
            ApiVersion = UmamiApiVersion.V1,
            ServerVersion = "1.40.0",
            DetectedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(UmamiApiVersion.V1, versionInfo.ApiVersion);
        Assert.Equal("1.40.0", versionInfo.ServerVersion);
        Assert.True(versionInfo.IsDetected);
        Assert.NotNull(versionInfo.DetectedAt);
    }

    [Fact]
    public void UmamiVersionInfo_V2_Detection()
    {
        // Arrange & Act
        var versionInfo = new UmamiVersionInfo
        {
            ApiVersion = UmamiApiVersion.V2,
            ServerVersion = "2.10.2",
            DetectedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(UmamiApiVersion.V2, versionInfo.ApiVersion);
        Assert.Equal("2.10.2", versionInfo.ServerVersion);
        Assert.True(versionInfo.IsDetected);
    }

    [Fact]
    public void UmamiVersionInfo_V3_Detection()
    {
        // Arrange & Act
        var versionInfo = new UmamiVersionInfo
        {
            ApiVersion = UmamiApiVersion.V3,
            ServerVersion = "3.0.1",
            DetectedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(UmamiApiVersion.V3, versionInfo.ApiVersion);
        Assert.Equal("3.0.1", versionInfo.ServerVersion);
        Assert.True(versionInfo.IsDetected);
    }

    [Theory]
    [InlineData(MetricType.path, "v2/v3 metric type")]
    [InlineData(MetricType.referrer, "common metric type")]
    [InlineData(MetricType.browser, "common metric type")]
    [InlineData(MetricType.os, "common metric type")]
    [InlineData(MetricType.device, "common metric type")]
    [InlineData(MetricType.country, "common metric type")]
    public async Task GetMetricsRequest_SupportedTypes(MetricType type, string description)
    {
        // Arrange
        var serviceProvider = GetServiceProvider();
        var umamiDataService = serviceProvider.GetRequiredService<UmamiDataService>();
        var metricsRequest = new MetricsRequest
        {
            StartAtDate = DateTime.Now.AddDays(-7),
            EndAtDate = DateTime.Now,
            Type = type,
            Unit = Unit.day
        };

        // Act
        var response = await umamiDataService.GetMetrics(metricsRequest);

        // Assert
        Assert.NotNull(response);
    }

    [Fact]
    public async Task MetricsRequest_WithOptionalUnit()
    {
        // Test that Unit is truly optional in v3
        var serviceProvider = GetServiceProvider();
        var umamiDataService = serviceProvider.GetRequiredService<UmamiDataService>();
        var metricsRequest = new MetricsRequest
        {
            StartAtDate = DateTime.Now.AddDays(-7),
            EndAtDate = DateTime.Now,
            Type = MetricType.path
            // Unit is not set - should work fine
        };

        var response = await umamiDataService.GetMetrics(metricsRequest);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task MetricsRequest_WithTimezone()
    {
        // Test timezone parameter (v3 feature)
        var serviceProvider = GetServiceProvider();
        var umamiDataService = serviceProvider.GetRequiredService<UmamiDataService>();
        var metricsRequest = new MetricsRequest
        {
            StartAtDate = DateTime.Now.AddDays(-7),
            EndAtDate = DateTime.Now,
            Type = MetricType.path,
            Unit = Unit.hour,
            Timezone = "UTC"
        };

        var response = await umamiDataService.GetMetrics(metricsRequest);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task MetricsRequest_WithLimit()
    {
        // Test limit parameter
        var serviceProvider = GetServiceProvider();
        var umamiDataService = serviceProvider.GetRequiredService<UmamiDataService>();
        var metricsRequest = new MetricsRequest
        {
            StartAtDate = DateTime.Now.AddDays(-7),
            EndAtDate = DateTime.Now,
            Type = MetricType.path,
            Unit = Unit.day,
            Limit = 10
        };

        var response = await umamiDataService.GetMetrics(metricsRequest);
        Assert.NotNull(response);
    }
}
