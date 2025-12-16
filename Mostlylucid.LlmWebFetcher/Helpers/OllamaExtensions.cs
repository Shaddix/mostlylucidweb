using System.Text;
using OllamaSharp.Models;

namespace Mostlylucid.LlmWebFetcher.Helpers;

/// <summary>
/// Result from streaming a generate response to completion.
/// </summary>
public class GenerateResult
{
    public string Response { get; set; } = "";
    public string? Model { get; set; }
    public bool Done { get; set; }
}

/// <summary>
/// Extension methods for working with OllamaSharp async streams.
/// </summary>
public static class OllamaExtensions
{
    /// <summary>
    /// Streams a generate response to completion, accumulating all response text.
    /// </summary>
    public static async Task<GenerateResult> StreamToEndAsync(
        this IAsyncEnumerable<GenerateResponseStream?> stream,
        CancellationToken cancellationToken = default)
    {
        var responseText = new StringBuilder();
        string? model = null;
        bool done = false;
        
        await foreach (var response in stream.WithCancellation(cancellationToken))
        {
            if (response?.Response != null)
            {
                responseText.Append(response.Response);
            }
            if (response?.Model != null)
            {
                model = response.Model;
            }
            done = response?.Done ?? false;
        }
        
        return new GenerateResult
        {
            Response = responseText.ToString(),
            Model = model,
            Done = done
        };
    }
}
