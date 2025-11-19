using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var token = "Bi7ym1BeYsNaBRmvPQWOv4toB7fuYkvPdh7JYHDiSy0PtrDKcpdI8fMabuemjBEiUddHCTH5ioMP+n9uhgPzH3hwYx0cbKshrB38Vq8XZDh8kDYL1lB7Y6w1XD8LUeeRcw8bAI6x5tJI2qXufO6MiTGx8IhIMUK4l/5kYBj6Vhv2xV9rrMQOZtDZXRJ1yuXuuDGTeab38TnEJGv3Dz+kmNOUENVgPcTE8oL82OKKmlOhJRNbYowH9sq7etetMzpNWafAipwcZH5lqyd4YCE6cUEiyHp3OpIWY3SkyCI0onaeemrl4OkOlAXoWYWPpJvj2Lm6gRvjtUtQ/M16psfv3X3R3dFkTGdk3YFMz20AWbO0HMrGzze+rznPQHx1";
        var websiteId = "1e3b7657-9487-4857-a9e9-4e1920aa8c42";

        var startAt = DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeMilliseconds();
        var endAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        // Test pageviews endpoint
        var pageviewsUrl = $"https://umami.mostlylucid.net/api/websites/{websiteId}/pageviews?startAt={startAt}&endAt={endAt}";
        Console.WriteLine($"Testing: {pageviewsUrl}");
        var response = await client.GetAsync(pageviewsUrl);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Response: {content.Substring(0, Math.Min(500, content.Length))}...");
        Console.WriteLine();

        // Test stats endpoint
        var statsUrl = $"https://umami.mostlylucid.net/api/websites/{websiteId}/stats?startAt={startAt}&endAt={endAt}";
        Console.WriteLine($"Testing: {statsUrl}");
        response = await client.GetAsync(statsUrl);
        content = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Response: {content.Substring(0, Math.Min(500, content.Length))}...");
    }
}
