#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.AspNetCore.WebUtilities, 9.0.0"

using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;

var queryParams = new Dictionary<string, string>
{
    {"startAt", "1730000000000"},
    {"endAt", "1730086400000"},
    {"type", "url"},
    {"unit", "day"},
    {"limit", "500"}
};

var queryString = QueryHelpers.AddQueryString(string.Empty, queryParams);
Console.WriteLine($"Query String: {queryString}");
Console.WriteLine($"\nExpected format: ?startAt=...&endAt=...&type=url&unit=day&limit=500");
