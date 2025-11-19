#!/usr/bin/env dotnet-script
#r "nuget: Umami.Net, 0.1.0"

using Umami.Net.UmamiData.Models.RequestObjects;
using Umami.Net.UmamiData.Helpers;
using System;

Console.WriteLine("=== Testing Query String Generation ===\n");

var endDate = DateTime.UtcNow;
var startDate = endDate.AddHours(-24);

Console.WriteLine($"Start: {startDate:O}");
Console.WriteLine($"End: {endDate:O}\n");

// Test with URL type
var request1 = new MetricsRequest
{
    StartAtDate = startDate,
    EndAtDate = endDate,
    Type = MetricType.url,
    Unit = Unit.day,
    Limit = 500
};

Console.WriteLine("Request with MetricType.url:");
Console.WriteLine($"  StartAt: {request1.StartAt}");
Console.WriteLine($"  EndAt: {request1.EndAt}");
var qs1 = request1.ToQueryString();
Console.WriteLine($"  Query string: {qs1}\n");

// Test with path type
var request2 = new MetricsRequest
{
    StartAtDate = startDate,
    EndAtDate = endDate,
    Type = MetricType.path,
    Unit = Unit.day,
    Limit = 500
};

Console.WriteLine("Request with MetricType.path:");
Console.WriteLine($"  StartAt: {request2.StartAt}");
Console.WriteLine($"  EndAt: {request2.EndAt}");
var qs2 = request2.ToQueryString();
Console.WriteLine($"  Query string: {qs2}\n");

Console.WriteLine("Full URLs:");
var websiteId = "32c2aa31-b1ac-44c0-b8f3-ff1f50403bee";
Console.WriteLine($"  With 'url': /api/websites/{websiteId}/metrics{qs1}");
Console.WriteLine($"  With 'path': /api/websites/{websiteId}/metrics{qs2}");
