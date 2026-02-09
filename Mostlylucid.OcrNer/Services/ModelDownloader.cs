using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.OcrNer.Config;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// Downloads and caches ONNX NER models and Tesseract tessdata on first use
/// </summary>
public class ModelDownloader
{
    private readonly ILogger<ModelDownloader> _logger;
    private readonly OcrNerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public ModelDownloader(
        ILogger<ModelDownloader> logger,
        IOptions<OcrNerConfig> config,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _config = config.Value;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Ensure all NER model files are downloaded and return their paths
    /// </summary>
    public async Task<NerModelPaths> EnsureNerModelAsync(CancellationToken ct = default)
    {
        var modelDir = Path.Combine(_config.ModelDirectory, "ner");
        Directory.CreateDirectory(modelDir);

        var modelPath = Path.Combine(modelDir, "model.onnx");
        var vocabPath = Path.Combine(modelDir, "vocab.txt");
        var configPath = Path.Combine(modelDir, "config.json");

        var baseUrl = $"https://huggingface.co/{_config.NerModelRepo}/resolve/main";

        var tasks = new List<Task>();

        if (!File.Exists(modelPath))
            tasks.Add(DownloadFileAsync($"{baseUrl}/model.onnx", modelPath, "NER model (~430MB)", ct));

        if (!File.Exists(vocabPath))
            tasks.Add(DownloadFileAsync($"{baseUrl}/vocab.txt", vocabPath, "vocab.txt", ct));

        if (!File.Exists(configPath))
            tasks.Add(DownloadFileAsync($"{baseUrl}/config.json", configPath, "config.json", ct));

        if (tasks.Count > 0)
        {
            _logger.LogInformation("First run: downloading BERT NER model from {Repo}. Models cached at: {Dir}",
                _config.NerModelRepo, modelDir);

            await Task.WhenAll(tasks);

            _logger.LogInformation("NER model downloaded successfully");
        }

        return new NerModelPaths(modelPath, vocabPath, configPath);
    }

    /// <summary>
    /// Ensure tessdata file is downloaded for the configured language
    /// </summary>
    public async Task<string> EnsureTessdataAsync(CancellationToken ct = default)
    {
        var tessdataDir = Path.Combine(_config.ModelDirectory, "tessdata");
        Directory.CreateDirectory(tessdataDir);

        var lang = _config.TesseractLanguage;
        var traineddataPath = Path.Combine(tessdataDir, $"{lang}.traineddata");

        if (!File.Exists(traineddataPath))
        {
            var url = string.Format(_config.TessdataUrlTemplate, lang);
            _logger.LogInformation("Downloading tessdata for language '{Lang}' from {Url}", lang, url);

            await DownloadFileAsync(url, traineddataPath, $"{lang}.traineddata", ct);

            _logger.LogInformation("Tessdata downloaded successfully");
        }

        return tessdataDir;
    }

    private async Task DownloadFileAsync(string url, string localPath, string description, CancellationToken ct)
    {
        var tempPath = localPath + ".tmp";

        try
        {
            _logger.LogDebug("Downloading {Description} from {Url}", description, url);

            using var httpClient = _httpClientFactory.CreateClient("OcrNerModelDownloader");
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long downloadedBytes = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloadedBytes += bytesRead;

                if (totalBytes > 0 && totalBytes > 10_000_000) // Log progress for large files
                {
                    var pct = (double)downloadedBytes / totalBytes.Value * 100;
                    if (downloadedBytes % (10 * 1024 * 1024) < 81920) // ~every 10MB
                        _logger.LogDebug("  {Description}: {Pct:F0}%", description, pct);
                }
            }

            // Ensure streams are flushed before move
            await fileStream.FlushAsync(ct);
            fileStream.Close();

            File.Move(tempPath, localPath, overwrite: true);
            _logger.LogDebug("Downloaded {Description} ({Bytes:N0} bytes)", description, downloadedBytes);
        }
        catch (Exception)
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }
}

/// <summary>
/// Paths to downloaded NER model files
/// </summary>
public record NerModelPaths(string ModelPath, string VocabPath, string ConfigPath);
