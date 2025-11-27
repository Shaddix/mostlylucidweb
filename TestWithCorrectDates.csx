#!/usr/bin/env dotnet-script
#r "nuget: Umami.Net, 0.1.0"
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.0"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"

using Umami.Net;
using Umami.Net.UmamiData;
using Umami.Net.UmamiData.Models.RequestObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var websiteId = "32c2aa31-b1ac-44c0-b8f3-ff1f50403bee";
var umamiPath = "https://umami.mostlylucid.net";
var username = "admin";
var password = Environment.GetEnvironmentVariable("UMAMI_PASSWORD") ?? "";

if (string.IsNullOrEmpty(password))
{
    Console.WriteLine("ERROR: Please set UMAMI_PASSWORD environment variable");
    return;
}

// Setup DI
var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

services.AddUmamiData(umamiPath, websiteId);

var serviceProvider = services.BuildServiceProvider();
var umamiDataService = serviceProvider.GetRequiredService<UmamiDataService>();

Console.WriteLine("=== Testing with HARDCODED CORRECT 2024 Dates ===\n");

// Login
Console.WriteLine("Logging in...");
var loginSuccess = await umamiDataService.LoginAsync(username, password);
if (!loginSuccess)
{
    Console.WriteLine("ERROR: Login failed!");
    return;
}
Console.WriteLine("✓ Login successful\n");

// Use HARDCODED correct 2024 timestamps
// November 18, 2024 00:00:00 UTC = 1731888000000 ms
// November 19, 2024 00:00:00 UTC = 1731974400000 ms

var startDate = new DateTime(2024, 11, 18, 0, 0, 0, DateTimeKind.Utc);
var endDate = new DateTime(2024, 11, 19, 0, 0, 0, DateTimeKind.Utc);

Console.WriteLine($"Using date range:");
Console.WriteLine($"  Start: {startDate:O}");
Console.WriteLine($"  End: {endDate:O}");

var metricsRequest = new MetricsRequest
{
    StartAtDate = startDate,
    EndAtDate = endDate,
    Type = MetricType.url,
    Unit = Unit.day,
    Limit = 10
};

Console.WriteLine($"\nMaking metrics request...");
Console.WriteLine($"  StartAt (ms): {metricsRequest.StartAt}");
Console.WriteLine($"  EndAt (ms): {metricsRequest.EndAt}");

var result = await umamiDataService.GetMetrics(metricsRequest);

Console.WriteLine($"\nResponse Status: {result?.Status}");

if (result?.Data != null && result.Data.Length > 0)
{
    Console.WriteLine($"✓ SUCCESS! Got {result.Data.Length} results");
    Console.WriteLine("\nTop URLs:");
    foreach (var metric in result.Data.Take(5))
    {
        Console.WriteLine($"  {metric.x}: {metric.y} views");
    }
}
else
{
    Console.WriteLine($"ERROR: {result?.Message ?? "Unknown error"}");
}
