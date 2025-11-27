#!/usr/bin/env dotnet-script

// Test timestamp conversion
var now = DateTime.UtcNow;
var yesterday = now.AddHours(-24);

Console.WriteLine($"Now: {now:O}");
Console.WriteLine($"Yesterday: {yesterday:O}");

// Simulate ToMilliseconds
var nowOffset = new DateTimeOffset(now.ToUniversalTime());
var yesterdayOffset = new DateTimeOffset(yesterday.ToUniversalTime());

var nowMs = nowOffset.ToUnixTimeMilliseconds();
var yesterdayMs = yesterdayOffset.ToUnixTimeMilliseconds();

Console.WriteLine($"\nNow in milliseconds: {nowMs}");
Console.WriteLine($"Yesterday in milliseconds: {yesterdayMs}");

// Convert back to verify
var nowConverted = DateTimeOffset.FromUnixTimeMilliseconds(nowMs);
var yesterdayConverted = DateTimeOffset.FromUnixTimeMilliseconds(yesterdayMs);

Console.WriteLine($"\nConverted back:");
Console.WriteLine($"Now: {nowConverted:O}");
Console.WriteLine($"Yesterday: {yesterdayConverted:O}");

// Check what 1763440087664 represents
var suspiciousTimestamp = 1763440087664;
var suspiciousDate = DateTimeOffset.FromUnixTimeMilliseconds(suspiciousTimestamp);
Console.WriteLine($"\nSuspicious timestamp {suspiciousTimestamp} represents: {suspiciousDate:O}");

// What should a typical timestamp look like?
var typical = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
var typicalOffset = new DateTimeOffset(typical);
var typicalMs = typicalOffset.ToUnixTimeMilliseconds();
Console.WriteLine($"\nA date in 2025: {typical:O}");
Console.WriteLine($"Its timestamp: {typicalMs}");
