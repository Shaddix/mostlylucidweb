using Microsoft.Extensions.DependencyInjection;
using Umami.Net.UmamiData;
using Umami.Net.UmamiData.Models.RequestObjects;

namespace Umami.Net.Test.UmamiData;

public class UmamiData_MetricsRequest_Test : UmamiDataBase
{
    [Fact]
    public async Task GetMetricsRequest_Path()
    {
        var serviceProvider = GetServiceProvider();
        var umamiDataService = serviceProvider.GetRequiredService<UmamiDataService>();
        var metricsRequest = new MetricsRequest
        {
            StartAtDate = DateTime.Now.AddDays(-7),
            EndAtDate = DateTime.Now,
            Type = MetricType.path, // v2/v3 use 'path' instead of 'url'
            Unit = Unit.day
        };
        var response = await umamiDataService.GetMetrics(metricsRequest);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetMetricsRequest_Url_Legacy()
    {
        // Test legacy v1 'url' type for backwards compatibility
        var serviceProvider = GetServiceProvider();
        var umamiDataService = serviceProvider.GetRequiredService<UmamiDataService>();
        var metricsRequest = new MetricsRequest
        {
            StartAtDate = DateTime.Now.AddDays(-7),
            EndAtDate = DateTime.Now,
            Type = MetricType.url, // v1 legacy
            Unit = Unit.day
        };
        var response = await umamiDataService.GetMetrics(metricsRequest);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetMetricsRequest_Events()
    {
        var serviceProvider = GetServiceProvider();
        var umamiDataService = serviceProvider.GetRequiredService<UmamiDataService>();
        var metricsRequest = new MetricsRequest
        {
            StartAtDate = DateTime.Now.AddDays(-7),
            EndAtDate = DateTime.Now,
            Type = MetricType.@event
        };
        var response = await umamiDataService.GetMetrics(metricsRequest);
        Assert.NotNull(response);
    }
}