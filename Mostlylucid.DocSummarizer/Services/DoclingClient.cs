using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Mostlylucid.DocSummarizer.Config;
using UglyToad.PdfPig;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Progress info for document conversion
/// </summary>
public class ConversionProgress
{
    public int TotalChunks { get; set; }
    public int CompletedChunks { get; set; }
    public int CurrentWave { get; set; }
    public int TotalWaves { get; set; }
    public string Status { get; set; } = "";
    public double Percent => TotalChunks > 0 ? (double)CompletedChunks / TotalChunks * 100 : 0;
}

public class DoclingClient : IDisposable
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(20);

    private readonly string _baseUrl;
    private readonly DoclingConfig _config;
    private readonly HttpClient _http;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _timeout;
    
    /// <summary>
    /// Progress callback - receives updates during conversion
    /// </summary>
    public Action<ConversionProgress>? OnProgress { get; set; }
    
    /// <summary>
    /// Chunk completion callback - fires when each chunk's markdown is ready.
    /// Use this for pipelined processing (e.g., start embedding while other chunks convert).
    /// Parameters: (chunkIndex, startPage, endPage, markdown)
    /// </summary>
    public Action<int, int, int, string>? OnChunkComplete { get; set; }

    public DoclingClient(DoclingConfig? config = null)
    {
        _config = config ?? new DoclingConfig();
        _baseUrl = _config.BaseUrl;
        _timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        _pollInterval = TimeSpan.FromSeconds(2);
        _http = new HttpClient { Timeout = _timeout + TimeSpan.FromMinutes(1) };
    }

    public DoclingClient(string baseUrl, TimeSpan? timeout = null)
        : this(new DoclingConfig { BaseUrl = baseUrl, TimeoutSeconds = (int)(timeout?.TotalSeconds ?? 1200) })
    {
    }

    public void Dispose() => _http.Dispose();
    
    private void Report(int completed, int total, int wave, int totalWaves, string status)
    {
        OnProgress?.Invoke(new ConversionProgress
        {
            CompletedChunks = completed,
            TotalChunks = total,
            CurrentWave = wave,
            TotalWaves = totalWaves,
            Status = status
        });
    }

    public async Task<string> ConvertAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Document not found: {filePath}");

        if (filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && _config.EnableSplitProcessing)
            return await ConvertPdfWithSplitProcessingAsync(filePath, cancellationToken);

        if (filePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) && _config.EnableSplitProcessing)
            return await ConvertDocxWithSplitProcessingAsync(filePath, cancellationToken);

        return await ConvertStandardAsync(filePath, cancellationToken);
    }

    private async Task<string> ConvertStandardAsync(string filePath, CancellationToken cancellationToken)
    {
        Report(0, 1, 1, 1, "Starting conversion...");
        
        var taskId = await StartConversionAsync(filePath, null, null, cancellationToken);
        var result = await WaitForCompletionAsync(taskId, cancellationToken);

        Report(1, 1, 1, 1, "Conversion complete");

        if (filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && IsGarbageText(result))
        {
            Report(1, 1, 1, 1, "Trying PdfPig fallback...");
            try
            {
                var pdfPigResult = ExtractWithPdfPig(filePath);
                if (!string.IsNullOrWhiteSpace(pdfPigResult) && !IsGarbageText(pdfPigResult))
                    return pdfPigResult;
            }
            catch { }
        }

        return result;
    }

    private async Task<string> ConvertPdfWithSplitProcessingAsync(string filePath, CancellationToken cancellationToken)
    {
        int totalPages;
        try
        {
            totalPages = GetPdfPageCount(filePath);
            Report(0, 1, 0, 1, $"PDF: {totalPages} pages");
        }
        catch (Exception ex)
        {
            Report(0, 1, 0, 1, $"Could not read PDF: {ex.Message}");
            return await ConvertStandardAsync(filePath, cancellationToken);
        }

        if (totalPages <= _config.PagesPerChunk)
        {
            Report(0, 1, 0, 1, "Small PDF - standard conversion");
            return await ConvertStandardAsync(filePath, cancellationToken);
        }

        var pagesPerChunk = _config.PagesPerChunk;
        var maxConcurrent = _config.MaxConcurrentChunks;
        var numChunks = (int)Math.Ceiling((double)totalPages / pagesPerChunk);
        var totalWaves = (int)Math.Ceiling((double)numChunks / maxConcurrent);

        var allChunks = new List<PdfChunkTask>();
        for (var i = 0; i < numChunks; i++)
        {
            var startPage = i * pagesPerChunk + 1;
            var endPage = Math.Min(startPage + pagesPerChunk - 1, totalPages);
            allChunks.Add(new PdfChunkTask(i, startPage, endPage, ""));
        }

        Report(0, numChunks, 0, totalWaves, $"Processing {numChunks} chunks ({pagesPerChunk} pages each)");

        var startTime = DateTime.UtcNow;
        var waveNumber = 0;
        var completedChunks = 0;

        for (var waveStart = 0; waveStart < allChunks.Count; waveStart += maxConcurrent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            waveNumber++;

            var waveChunks = allChunks.Skip(waveStart).Take(maxConcurrent).ToList();
            var waveDesc = string.Join(", ", waveChunks.Select(c => $"p{c.StartPage}-{c.EndPage}"));
            
            Report(completedChunks, numChunks, waveNumber, totalWaves, $"Wave {waveNumber}/{totalWaves}: {waveDesc}");

            // Submit this wave
            foreach (var chunk in waveChunks)
            {
                try
                {
                    chunk.TaskId = await StartConversionAsync(filePath, chunk.StartPage, chunk.EndPage, cancellationToken);
                }
                catch
                {
                    chunk.IsFailed = true;
                }
            }

            // Poll for this wave to complete
            var pendingChunks = waveChunks.Where(c => !string.IsNullOrEmpty(c.TaskId) && !c.IsFailed).ToList();
            while (pendingChunks.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed > _timeout)
                    throw new TimeoutException($"Split processing timed out after {_timeout.TotalMinutes:F0} minutes");

                await Task.Delay(_pollInterval, cancellationToken);

                foreach (var chunk in pendingChunks.ToList())
                {
                    var status = await CheckTaskStatusAsync(chunk.TaskId, cancellationToken);

                        if (status == "SUCCESS")
                        {
                            chunk.IsComplete = true;
                            chunk.Result = await GetResultAsync(chunk.TaskId, cancellationToken);
                            pendingChunks.Remove(chunk);
                            completedChunks++;
                            
                            Report(completedChunks, numChunks, waveNumber, totalWaves, 
                                $"Wave {waveNumber}/{totalWaves}: {completedChunks}/{numChunks} chunks done");
                            
                            // Fire chunk completion callback for pipelined processing
                            if (OnChunkComplete != null && !string.IsNullOrEmpty(chunk.Result))
                            {
                                try
                                {
                                    OnChunkComplete(chunk.Index, chunk.StartPage, chunk.EndPage, chunk.Result);
                                }
                                catch
                                {
                                    // Don't let callback errors break conversion
                                }
                            }
                        }
                    else if (status == "FAILURE" || status == "REVOKED")
                    {
                        chunk.IsFailed = true;
                        pendingChunks.Remove(chunk);
                        completedChunks++; // Count failures too for progress
                        
                        Report(completedChunks, numChunks, waveNumber, totalWaves,
                            $"Wave {waveNumber}/{totalWaves}: chunk failed");
                    }
                }
            }
        }

        var totalElapsed = DateTime.UtcNow - startTime;
        var successCount = allChunks.Count(c => c.IsComplete);
        Report(numChunks, numChunks, totalWaves, totalWaves, 
            $"Converted {successCount}/{numChunks} chunks in {totalElapsed.TotalSeconds:F0}s");

        var orderedChunks = allChunks
            .Where(c => c.IsComplete && !string.IsNullOrEmpty(c.Result))
            .OrderBy(c => c.StartPage)
            .ToList();

        if (orderedChunks.Count == 0) 
            throw new Exception("No chunks were successfully converted");

        // Inject page markers into markdown so chunker can extract page numbers
        var combinedMarkdown = string.Join("\n\n---\n\n", orderedChunks.Select(c => 
            $"<!-- PAGE:{c.StartPage}-{c.EndPage} -->\n{c.Result}"));

        if (IsGarbageText(combinedMarkdown))
        {
            Report(numChunks, numChunks, totalWaves, totalWaves, "Trying PdfPig fallback...");
            try
            {
                var pdfPigResult = ExtractWithPdfPig(filePath);
                if (!string.IsNullOrWhiteSpace(pdfPigResult) && !IsGarbageText(pdfPigResult))
                    return pdfPigResult;
            }
            catch { }
        }

        return combinedMarkdown;
    }

    private async Task<string> ConvertDocxWithSplitProcessingAsync(string filePath, CancellationToken cancellationToken)
    {
        List<DocxChapter> chapters;
        try
        {
            chapters = GetDocxChapters(filePath);
            Report(0, 1, 0, 1, $"DOCX: {chapters.Count} chapters/sections");
        }
        catch (Exception ex)
        {
            Report(0, 1, 0, 1, $"Could not read DOCX: {ex.Message}");
            return await ConvertStandardAsync(filePath, cancellationToken);
        }

        if (chapters.Count <= 3)
        {
            Report(0, 1, 0, 1, "Small DOCX - standard conversion");
            return await ConvertStandardAsync(filePath, cancellationToken);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"docsummarizer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var maxConcurrent = _config.MaxConcurrentChunks;
            var totalWaves = (int)Math.Ceiling((double)chapters.Count / maxConcurrent);
            
            Report(0, chapters.Count, 0, totalWaves, $"Processing {chapters.Count} chapters");

            var chunkTasks = new List<DocxChunkTask>();
            var startTime = DateTime.UtcNow;

            for (var i = 0; i < chapters.Count; i++)
            {
                var chapter = chapters[i];
                var tempPath = Path.Combine(tempDir, $"chapter_{i:D3}.docx");
                CreateDocxFromChapter(filePath, chapter, tempPath);
                chunkTasks.Add(new DocxChunkTask(i, chapter.Title, tempPath));
            }

            var waveNumber = 0;
            var completedChunks = 0;

            for (var waveStart = 0; waveStart < chunkTasks.Count; waveStart += maxConcurrent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                waveNumber++;

                var waveChunks = chunkTasks.Skip(waveStart).Take(maxConcurrent).ToList();
                var chapterNames = string.Join(", ", waveChunks.Select(c => 
                    c.Title.Length > 15 ? c.Title[..15] + "..." : c.Title));
                
                Report(completedChunks, chapters.Count, waveNumber, totalWaves, 
                    $"Wave {waveNumber}/{totalWaves}: {chapterNames}");

                foreach (var chunk in waveChunks)
                {
                    try
                    {
                        chunk.TaskId = await StartConversionAsync(chunk.TempPath, null, null, cancellationToken);
                    }
                    catch
                    {
                        chunk.IsFailed = true;
                    }
                }

                var pendingChunks = waveChunks.Where(c => !string.IsNullOrEmpty(c.TaskId) && !c.IsFailed).ToList();
                while (pendingChunks.Any())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var elapsed = DateTime.UtcNow - startTime;
                    if (elapsed > _timeout)
                        throw new TimeoutException($"Split processing timed out after {_timeout.TotalMinutes:F0} minutes");

                    await Task.Delay(_pollInterval, cancellationToken);

                    foreach (var chunk in pendingChunks.ToList())
                    {
                        var status = await CheckTaskStatusAsync(chunk.TaskId, cancellationToken);

                        if (status == "SUCCESS")
                        {
                            chunk.IsComplete = true;
                            chunk.Result = await GetResultAsync(chunk.TaskId, cancellationToken);
                            pendingChunks.Remove(chunk);
                            completedChunks++;
                            
                            Report(completedChunks, chapters.Count, waveNumber, totalWaves,
                                $"Wave {waveNumber}/{totalWaves}: {completedChunks}/{chapters.Count} chapters done");
                            
                            // Fire chunk completion callback for pipelined processing (DOCX)
                            if (OnChunkComplete != null && !string.IsNullOrEmpty(chunk.Result))
                            {
                                try
                                {
                                    OnChunkComplete(chunk.Index, chunk.Index, chunk.Index, chunk.Result);
                                }
                                catch
                                {
                                    // Don't let callback errors break conversion
                                }
                            }
                        }
                        else if (status == "FAILURE" || status == "REVOKED")
                        {
                            chunk.IsFailed = true;
                            pendingChunks.Remove(chunk);
                            completedChunks++;
                            
                            Report(completedChunks, chapters.Count, waveNumber, totalWaves,
                                $"Wave {waveNumber}/{totalWaves}: chapter failed");
                        }
                    }
                }
            }

            var totalElapsed = DateTime.UtcNow - startTime;
            var successCount = chunkTasks.Count(c => c.IsComplete);
            Report(chapters.Count, chapters.Count, totalWaves, totalWaves,
                $"Converted {successCount}/{chapters.Count} chapters in {totalElapsed.TotalSeconds:F0}s");

            var orderedChunks = chunkTasks
                .Where(c => c.IsComplete && !string.IsNullOrEmpty(c.Result))
                .OrderBy(c => c.Index)
                .ToList();

            if (orderedChunks.Count == 0) 
                throw new Exception("No chapters were successfully converted");

            var sb = new StringBuilder();
            foreach (var chunk in orderedChunks)
            {
                if (sb.Length > 0) sb.AppendLine("\n---\n");
                sb.AppendLine(chunk.Result);
            }

            return sb.ToString();
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private async Task<string> WaitForCompletionAsync(string taskId, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        var startTime = DateTime.UtcNow;

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            try
            {
                var elapsed = DateTime.UtcNow - startTime;
                var statusResponse = await _http.GetAsync($"{_baseUrl}/v1/status/poll/{taskId}", timeoutCts.Token);

                if (!statusResponse.IsSuccessStatusCode)
                {
                    await Task.Delay(_pollInterval, timeoutCts.Token);
                    continue;
                }

                var statusJson = await statusResponse.Content.ReadAsStringAsync(timeoutCts.Token);
                var status = JsonSerializer.Deserialize(statusJson, DocSummarizerJsonContext.Default.DoclingStatusResponse);
                var taskStatus = status?.TaskStatus?.ToUpperInvariant();

                if (taskStatus == "SUCCESS")
                    return await GetResultAsync(taskId, timeoutCts.Token);

                if (taskStatus == "FAILURE" || taskStatus == "REVOKED")
                    throw new Exception($"Docling conversion failed: {status?.TaskStatus}");

                Report(0, 1, 1, 1, $"Converting... {taskStatus} ({elapsed.TotalSeconds:F0}s)");
                await Task.Delay(_pollInterval, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Document conversion timed out after {_timeout.TotalMinutes:F0} minutes");
            }
        }

        throw new TimeoutException($"Document conversion timed out after {_timeout.TotalMinutes:F0} minutes");
    }

    private static int GetPdfPageCount(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        return document.NumberOfPages;
    }

    private static bool IsGarbageText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var sample = text.Length > 2000 ? text[..2000] : text;
        var alphaCount = sample.Count(char.IsLetter);
        var upperCount = sample.Count(char.IsUpper);

        if (alphaCount == 0) return false;

        var upperRatio = (double)upperCount / alphaCount;
        if (upperRatio > 0.4 && alphaCount > 50) return true;

        var letterFreq = sample
            .Where(char.IsLetter)
            .GroupBy(char.ToLower)
            .ToDictionary(g => g.Key, g => g.Count());

        if (letterFreq.Count > 0)
        {
            var avgFreq = (double)alphaCount / letterFreq.Count;
            var variance = letterFreq.Values.Average(v => Math.Pow(v - avgFreq, 2));
            var stdDev = Math.Sqrt(variance);
            if (avgFreq > 5 && stdDev / avgFreq > 2.5) return true;
        }

        var vowelCount = sample.Count(c => "aeiouAEIOU".Contains(c));
        var vowelRatio = (double)vowelCount / alphaCount;
        if (vowelRatio < 0.15 && alphaCount > 50) return true;

        return false;
    }

    private static string ExtractWithPdfPig(string filePath)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                }
                sb.AppendLine(text);
            }
        }

        return sb.ToString();
    }

    private static List<DocxChapter> GetDocxChapters(string filePath)
    {
        var chapters = new List<DocxChapter>();
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return chapters;

        var elements = body.Elements().ToList();
        var currentChapterStart = 0;
        string? currentTitle = null;

        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i] is Paragraph para)
            {
                var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                if (styleId != null && (styleId.StartsWith("Heading1", StringComparison.OrdinalIgnoreCase) ||
                                        styleId.Equals("Title", StringComparison.OrdinalIgnoreCase) ||
                                        styleId.StartsWith("Heading2", StringComparison.OrdinalIgnoreCase)))
                {
                    if (currentTitle != null && i > currentChapterStart)
                        chapters.Add(new DocxChapter(currentTitle, currentChapterStart, i - 1));

                    currentTitle = GetParagraphText(para);
                    if (string.IsNullOrWhiteSpace(currentTitle))
                        currentTitle = $"Section {chapters.Count + 1}";
                    currentChapterStart = i;
                }
            }
        }

        if (currentTitle != null)
            chapters.Add(new DocxChapter(currentTitle, currentChapterStart, elements.Count - 1));
        else if (elements.Count > 0)
            chapters.Add(new DocxChapter("Document", 0, elements.Count - 1));

        return chapters;
    }

    private static string GetParagraphText(Paragraph para)
    {
        var sb = new StringBuilder();
        foreach (var run in para.Elements<Run>())
            foreach (var text in run.Elements<Text>())
                sb.Append(text.Text);
        return sb.ToString().Trim();
    }

    private static void CreateDocxFromChapter(string sourcePath, DocxChapter chapter, string destPath)
    {
        File.Copy(sourcePath, destPath, true);
        using var doc = WordprocessingDocument.Open(destPath, true);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return;

        var elements = body.Elements().ToList();
        for (var i = elements.Count - 1; i >= 0; i--)
            if (i < chapter.StartIndex || i > chapter.EndIndex)
                elements[i].Remove();

        doc.MainDocumentPart?.Document?.Save();
    }

    private async Task<string?> CheckTaskStatusAsync(string taskId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/v1/status/poll/{taskId}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var status = JsonSerializer.Deserialize(json, DocSummarizerJsonContext.Default.DoclingStatusResponse);
            return status?.TaskStatus?.ToUpperInvariant();
        }
        catch { return null; }
    }

    private async Task<string> StartConversionAsync(string filePath, int? startPage, int? endPage, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(filePath);
        var streamContent = new StreamContent(stream);
        content.Add(streamContent, "files", Path.GetFileName(filePath));

        if (filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_config.PdfBackend))
            content.Add(new StringContent(_config.PdfBackend), "pdf_backend");

        if (startPage.HasValue && endPage.HasValue)
        {
            content.Add(new StringContent(startPage.Value.ToString()), "page_range");
            content.Add(new StringContent(endPage.Value.ToString()), "page_range");
        }

        var response = await _http.PostAsync($"{_baseUrl}/v1/convert/file/async", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var taskResponse = JsonSerializer.Deserialize(json, DocSummarizerJsonContext.Default.DoclingTaskResponse);

        return taskResponse?.TaskId ?? throw new Exception("No task ID returned from Docling");
    }

    private async Task<string> GetResultAsync(string taskId, CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync($"{_baseUrl}/v1/result/{taskId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize(json, DocSummarizerJsonContext.Default.DoclingResultResponse);

        return result?.Document?.MdContent ?? throw new Exception("No markdown content returned from Docling");
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private record DocxChapter(string Title, int StartIndex, int EndIndex);

    private class DocxChunkTask
    {
        public DocxChunkTask(int index, string title, string tempPath)
        {
            Index = index;
            Title = title;
            TempPath = tempPath;
        }

        public int Index { get; }
        public string Title { get; }
        public string TempPath { get; }
        public string TaskId { get; set; } = "";
        public bool IsComplete { get; set; }
        public bool IsFailed { get; set; }
        public string? Result { get; set; }
    }

    private class PdfChunkTask
    {
        public PdfChunkTask(int index, int startPage, int endPage, string taskId)
        {
            Index = index;
            StartPage = startPage;
            EndPage = endPage;
            TaskId = taskId;
        }

        public int Index { get; }
        public int StartPage { get; }
        public int EndPage { get; }
        public string TaskId { get; set; }
        public bool IsComplete { get; set; }
        public bool IsFailed { get; set; }
        public string? Result { get; set; }
    }
}

public class DoclingTaskResponse
{
    [JsonPropertyName("task_id")] public string? TaskId { get; set; }
}

public class DoclingStatusResponse
{
    [JsonPropertyName("task_id")] public string? TaskId { get; set; }
    [JsonPropertyName("task_status")] public string? TaskStatus { get; set; }
}

public class DoclingResultResponse
{
    [JsonPropertyName("document")] public DoclingDocument? Document { get; set; }
}

public class DoclingDocument
{
    [JsonPropertyName("md_content")] public string? MdContent { get; set; }
}

public class DoclingResponse
{
    [JsonPropertyName("document")] public DoclingDocument? Document { get; set; }
}
