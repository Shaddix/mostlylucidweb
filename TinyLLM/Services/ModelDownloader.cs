using System.IO;
using System.Net.Http;

namespace TinyLLM.Services;

public class ModelDownloader
{
    private readonly HttpClient _httpClient;
    private readonly string _modelsDirectory;

    public ModelDownloader()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        _modelsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
        Directory.CreateDirectory(_modelsDirectory);
    }

    public async Task<string> DownloadModelAsync(
        string modelUrl,
        string modelName,
        IProgress<(long bytesReceived, long? totalBytes, double percentage)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var modelPath = Path.Combine(_modelsDirectory, modelName);

        // Check if model already exists
        if (File.Exists(modelPath))
        {
            var fileInfo = new FileInfo(modelPath);
            if (fileInfo.Length > 0)
            {
                progress?.Report((fileInfo.Length, fileInfo.Length, 100));
                return modelPath;
            }
        }

        // Download the model
        using var response = await _httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        var buffer = new byte[8192];
        long bytesReceived = 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        while (true)
        {
            var bytesRead = await contentStream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0) break;

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            bytesReceived += bytesRead;

            if (totalBytes.HasValue && progress != null)
            {
                var percentage = (double)bytesReceived / totalBytes.Value * 100;
                progress.Report((bytesReceived, totalBytes, percentage));
            }
        }

        return modelPath;
    }

    public async Task<string> EnsureModelDownloadedAsync(
        IProgress<(long bytesReceived, long? totalBytes, double percentage)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Using Gemma 2 2B GGUF (Q4_K_M quantization, ~1.7GB) - More capable than TinyLlama
        const string modelUrl = "https://huggingface.co/lmstudio-community/gemma-2-2b-it-GGUF/resolve/main/gemma-2-2b-it-Q4_K_M.gguf";
        const string modelName = "gemma-2-2b-it-Q4_K_M.gguf";

        return await DownloadModelAsync(modelUrl, modelName, progress, cancellationToken);
    }

    public List<string> GetAvailableModels()
    {
        if (!Directory.Exists(_modelsDirectory))
            return new List<string>();

        return Directory.GetFiles(_modelsDirectory, "*.gguf")
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Cast<string>()
            .ToList();
    }
}
