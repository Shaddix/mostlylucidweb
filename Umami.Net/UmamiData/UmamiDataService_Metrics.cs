using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Umami.Net.UmamiData.Helpers;
using Umami.Net.UmamiData.Models;
using Umami.Net.UmamiData.Models.RequestObjects;
using Umami.Net.UmamiData.Models.ResponseObjects;

namespace Umami.Net.UmamiData;

public partial class UmamiDataService
{
    /// <summary>
    ///     Gets the Metrics for the website from Umami
    /// </summary>
    /// <param name="metricsRequest"> An object which allows you to set the QueryString parameters.</param>
    /// <returns></returns>
    public async Task<UmamiResult<MetricsResponseModels[]>> GetMetrics(MetricsRequest metricsRequest)
    {
        try
        {
            if (await authService.Login() == false)
                return new UmamiResult<MetricsResponseModels[]>(HttpStatusCode.Unauthorized, "Failed to login", null);
            // Start building the query string
            var queryString = metricsRequest.ToQueryString();

            // Make the HTTP request
            var response = await authService.HttpClient.GetAsync($"/api/websites/{WebsiteId}/metrics{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<MetricsResponseModels[]>();
                return new UmamiResult<MetricsResponseModels[]>(response.StatusCode, response.ReasonPhrase ?? "Success",
                    content ?? Array.Empty<MetricsResponseModels>());
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await authService.Login();
                return await GetMetrics(metricsRequest);
            }

            logger.LogError("Failed to get metrics");
            return new UmamiResult<MetricsResponseModels[]>(response.StatusCode,
                response.ReasonPhrase ?? "Failed to get metrics", null);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get metrics");
            return new UmamiResult<MetricsResponseModels[]>(HttpStatusCode.InternalServerError, "Failed to get metrics",
                null);
        }
    }

    /// <summary>
    /// Gets expanded metrics with detailed engagement data for the website from Umami
    /// </summary>
    /// <param name="metricsRequest">An object which allows you to set the QueryString parameters</param>
    /// <returns>Array of expanded metrics with pageviews, visitors, visits, bounces, and totaltime</returns>
    public async Task<UmamiResult<ExpandedMetricsResponseModel[]>> GetExpandedMetrics(MetricsRequest metricsRequest)
    {
        try
        {
            if (await authService.Login() == false)
                return new UmamiResult<ExpandedMetricsResponseModel[]>(HttpStatusCode.Unauthorized, "Failed to login", null);

            var queryString = metricsRequest.ToQueryString();
            var response = await authService.HttpClient.GetAsync($"/api/websites/{WebsiteId}/metrics/expanded{queryString}");

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Successfully got expanded metrics");
                var content = await response.Content.ReadFromJsonAsync<ExpandedMetricsResponseModel[]>();
                return new UmamiResult<ExpandedMetricsResponseModel[]>(
                    response.StatusCode,
                    response.ReasonPhrase ?? "Success",
                    content ?? Array.Empty<ExpandedMetricsResponseModel>());
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await authService.Login();
                return await GetExpandedMetrics(metricsRequest);
            }

            logger.LogError("Failed to get expanded metrics");
            return new UmamiResult<ExpandedMetricsResponseModel[]>(
                response.StatusCode,
                response.ReasonPhrase ?? "Failed to get expanded metrics",
                null);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get expanded metrics");
            return new UmamiResult<ExpandedMetricsResponseModel[]>(
                HttpStatusCode.InternalServerError,
                "Failed to get expanded metrics",
                null);
        }
    }
}