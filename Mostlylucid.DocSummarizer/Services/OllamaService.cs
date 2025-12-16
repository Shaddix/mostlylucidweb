using System.Text;
using OllamaSharp;
using OllamaSharp.Models;

namespace Mostlylucid.DocSummarizer.Services;

public class OllamaService
{
    private readonly OllamaApiClient _client;
    private readonly string _model;
    private readonly string _embedModel;

    public OllamaService(
        string model = "llama3.2:3b",
        string embedModel = "nomic-embed-text",
        string baseUrl = "http://localhost:11434")
    {
        _client = new OllamaApiClient(new Uri(baseUrl));
        _model = model;
        _embedModel = embedModel;
    }

    public async Task<string> GenerateAsync(string prompt, double temperature = 0.3)
    {
        var request = new GenerateRequest
        {
            Model = _model,
            Prompt = prompt,
            Options = new RequestOptions { Temperature = (float)temperature }
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _client.GenerateAsync(request))
        {
            if (chunk?.Response != null)
                sb.Append(chunk.Response);
        }
        return sb.ToString().Trim();
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var request = new EmbedRequest
        {
            Model = _embedModel,
            Input = [text]
        };
        
        var response = await _client.EmbedAsync(request);
        return response.Embeddings.First().Select(d => (float)d).ToArray();
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var models = await _client.ListLocalModelsAsync();
            return models.Any();
        }
        catch
        {
            return false;
        }
    }
}
