#!/usr/bin/env dotnet-script
#r "nuget: System.Net.Http, 8.0.0"

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

// Test direct API call
var websiteId = "32c2aa31-b1ac-44c0-b8f3-ff1f50403bee";
var umamiPath = "https://umami.mostlylucid.net";
var username = "admin";
var password = Environment.GetEnvironmentVariable("UMAMI_PASSWORD") ?? "";

if (string.IsNullOrEmpty(password))
{
    Console.WriteLine("ERROR: Please set UMAMI_PASSWORD environment variable");
    return;
}

var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(umamiPath);

// Login first
var loginPayload = new { username, password };
var loginResponse = await httpClient.PostAsJsonAsync("/api/auth/login", loginPayload);

if (!loginResponse.IsSuccessStatusCode)
{
    Console.WriteLine($"Login failed: {loginResponse.StatusCode}");
    return;
}

var loginContent = await loginResponse.Content.ReadAsStringAsync();
Console.WriteLine($"Login successful: {loginContent}");

// Extract token
var loginJson = JsonDocument.Parse(loginContent);
var token = loginJson.RootElement.GetProperty("token").GetString();
httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

// Calculate timestamps
var now = DateTime.UtcNow;
var yesterday = now.AddHours(-24);

// Manual timestamp calculation
var nowMs = ((DateTimeOffset)now).ToUnixTimeMilliseconds();
var yesterdayMs = ((DateTimeOffset)yesterday).ToUnixTimeMilliseconds();

Console.WriteLine($"\nTimestamp calculation:");
Console.WriteLine($"Now: {now:O} => {nowMs}");
Console.WriteLine($"Yesterday: {yesterday:O} => {yesterdayMs}");

// Try API call with these timestamps
var testUrl = $"/api/websites/{websiteId}/metrics?startAt={yesterdayMs}&endAt={nowMs}&type=url&unit=day&limit=10";

Console.WriteLine($"\nTesting URL: {testUrl}");

var response = await httpClient.GetAsync(testUrl);
Console.WriteLine($"Response status: {response.StatusCode}");

var responseBody = await response.Content.ReadAsStringAsync();
Console.WriteLine($"Response body: {responseBody}");
