using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

class TestLiveTranslation
{
    private record PostRecord(
        string target_lang,
        string[] text,
        string source_lang = "en",
        bool perform_sentence_splitting = false);

    private record PostResponse(string target_lang, string[] translated, string source_lang, float translation_time);

    static async Task Main(string[] args)
    {
        var client = new HttpClient();

        Console.WriteLine("Testing live translation service on port 8000...\n");

        // Test 1: Trailing space
        await TestTranslation(client, "Trailing space ", "Test 1: Trailing space");

        // Test 2: Leading space
        await TestTranslation(client, " Leading space", "Test 2: Leading space");

        // Test 3: Both spaces
        await TestTranslation(client, " Both spaces ", "Test 3: Both spaces");

        // Test 4: Multiple spaces
        await TestTranslation(client, "Test.  Two spaces", "Test 4: Multiple spaces after period");

        // Test 5: Original issue - bold text with spaces
        await TestTranslation(client, "Test. New Text ", "Test 5: Period with trailing space");

        Console.WriteLine("\nAll tests completed!");
    }

    static async Task TestTranslation(HttpClient client, string input, string testName)
    {
        try
        {
            var request = new PostRecord("es", new[] { input });
            var response = await client.PostAsJsonAsync("http://localhost:8000/translate", request);
            var result = await response.Content.ReadFromJsonAsync<PostResponse>();

            var output = result.translated[0];

            Console.WriteLine($"{testName}:");
            Console.WriteLine($"  Input:  '{input}' (length: {input.Length})");
            Console.WriteLine($"  Output: '{output}' (length: {output.Length})");
            Console.WriteLine($"  Leading space preserved: {(input.StartsWith(" ") == output.StartsWith(" ") ? "✓" : "✗")}");
            Console.WriteLine($"  Trailing space preserved: {(input.EndsWith(" ") == output.EndsWith(" ") ? "✓" : "✗")}");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{testName} FAILED: {ex.Message}\n");
        }
    }
}
