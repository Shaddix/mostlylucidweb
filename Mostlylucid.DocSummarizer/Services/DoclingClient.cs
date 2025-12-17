using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Mostlylucid.DocSummarizer.Config;
using UglyToad.PdfPig;

namespace Mostlylucid.DocSummarizer.Services;

public class DoclingClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _pollInterval;
    private readonly DoclingConfig _config;

    /// <summary>
    /// Default timeout for document conversion (10 minutes for large PDFs)
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    public DoclingClient(DoclingConfig? config = null)
    {
        _config = config ?? new DoclingConfig();
        _baseUrl = _config.BaseUrl;
        _timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        _pollInterval = TimeSpan.FromSeconds(2);
        _http = new HttpClient { Timeout = _timeout + TimeSpan.FromMinutes(1) }; // Extra buffer for HTTP timeout
    }
    
    // Legacy constructor for backwards compatibility
    public DoclingClient(string baseUrl, TimeSpan? timeout = null)
        : this(new DoclingConfig { BaseUrl = baseUrl, TimeoutSeconds = (int)(timeout?.TotalSeconds ?? 300) })
    {
    }

    public async Task<string> ConvertAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Document not found: {filePath}");

        // For PDFs, use split processing for better progress feedback (if enabled)
        if (filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && _config.EnableSplitProcessing)
        {
            return await ConvertPdfWithSplitProcessingAsync(filePath, cancellationToken);
        }
        
        // For DOCX, use split processing by chapters for large files
        if (filePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) && _config.EnableSplitProcessing)
        {
            return await ConvertDocxWithSplitProcessingAsync(filePath, cancellationToken);
        }
        
        // For other formats, use standard conversion
        return await ConvertStandardAsync(filePath, cancellationToken);
    }

    private async Task<string> ConvertStandardAsync(string filePath, CancellationToken cancellationToken)
    {
        // Start the async conversion
        var taskId = await StartConversionAsync(filePath, null, null, cancellationToken);
        
        // Poll for completion
        var result = await WaitForCompletionAsync(taskId, "Converting", cancellationToken);
        
        return result;
    }

    /// <summary>
    /// Get the number of pages in a PDF using PdfPig
    /// </summary>
    private static int GetPdfPageCount(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        return document.NumberOfPages;
    }

    /// <summary>
    /// Convert PDF using split processing for better parallelism and progress feedback
    /// </summary>
    private async Task<string> ConvertPdfWithSplitProcessingAsync(string filePath, CancellationToken cancellationToken)
    {
        // Get actual page count first
        int totalPages;
        try
        {
            totalPages = GetPdfPageCount(filePath);
            Console.WriteLine($"PDF has {totalPages} pages");
            Console.Out.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not read PDF page count: {ex.Message}");
            Console.WriteLine("Falling back to standard conversion...");
            return await ConvertStandardAsync(filePath, cancellationToken);
        }
        
        // For small PDFs, just convert the whole thing
        if (totalPages <= _config.PagesPerChunk)
        {
            Console.WriteLine("Small PDF - using standard conversion...");
            return await ConvertStandardAsync(filePath, cancellationToken);
        }
        
        var pagesPerChunk = _config.PagesPerChunk;
        var maxConcurrent = _config.MaxConcurrentChunks;
        var numChunks = (int)Math.Ceiling((double)totalPages / pagesPerChunk);
        
        Console.WriteLine($"Split processing: {numChunks} chunks ({pagesPerChunk} pages each, max {maxConcurrent} concurrent)");
        Console.Out.Flush();
        
        var allChunks = new List<PdfChunkTask>();
        var startTime = DateTime.UtcNow;
        
        // Create all chunk definitions upfront
        for (int i = 0; i < numChunks; i++)
        {
            var startPage = i * pagesPerChunk + 1;
            var endPage = Math.Min(startPage + pagesPerChunk - 1, totalPages);
            allChunks.Add(new PdfChunkTask(i, startPage, endPage, ""));
        }
        
        // Process in waves of maxConcurrent
        for (int waveStart = 0; waveStart < allChunks.Count; waveStart += maxConcurrent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var waveChunks = allChunks.Skip(waveStart).Take(maxConcurrent).ToList();
            
            // Submit this wave
            Console.Write($"Wave {waveStart / maxConcurrent + 1}: ");
            foreach (var chunk in waveChunks)
            {
                try
                {
                    chunk.TaskId = await StartConversionAsync(filePath, chunk.StartPage, chunk.EndPage, cancellationToken);
                    Console.Write($"[{chunk.Index}:p{chunk.StartPage}-{chunk.EndPage}]");
                    Console.Out.Flush();
                }
                catch (Exception ex)
                {
                    chunk.IsFailed = true;
                    Console.Write($"[{chunk.Index}:submit-fail]");
                    Console.Out.Flush();
                }
            }
            
            // Poll for this wave to complete
            var pendingChunks = waveChunks.Where(c => !string.IsNullOrEmpty(c.TaskId) && !c.IsFailed).ToList();
            while (pendingChunks.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed > _timeout)
                {
                    throw new TimeoutException($"Split processing timed out after {_timeout.TotalMinutes:F0} minutes");
                }
                
                await Task.Delay(_pollInterval, cancellationToken);
                
                foreach (var chunk in pendingChunks.ToList())
                {
                    var status = await CheckTaskStatusAsync(chunk.TaskId, cancellationToken);
                    
                    if (status == "SUCCESS")
                    {
                        chunk.IsComplete = true;
                        chunk.Result = await GetResultAsync(chunk.TaskId, cancellationToken);
                        Console.Write($"[{chunk.Index}:ok]");
                        Console.Out.Flush();
                        pendingChunks.Remove(chunk);
                    }
                    else if (status == "FAILURE" || status == "REVOKED")
                    {
                        chunk.IsFailed = true;
                        Console.Write($"[{chunk.Index}:fail]");
                        Console.Out.Flush();
                        pendingChunks.Remove(chunk);
                    }
                }
                
                if (pendingChunks.Any())
                {
                    Console.Write(".");
                    Console.Out.Flush();
                }
            }
            
            Console.WriteLine();
        }
        
        var totalElapsed = DateTime.UtcNow - startTime;
        var successCount = allChunks.Count(c => c.IsComplete);
        Console.WriteLine($"Conversion complete: {successCount}/{numChunks} chunks in {totalElapsed.TotalSeconds:F0}s");
        
        // Concatenate successful results in order
        var orderedChunks = allChunks
            .Where(c => c.IsComplete && !string.IsNullOrEmpty(c.Result))
            .OrderBy(c => c.StartPage)
            .ToList();
        
        if (orderedChunks.Count == 0)
        {
            throw new Exception("No chunks were successfully converted");
        }
        
        // Simple markdown concatenation
        var combinedMarkdown = string.Join("\n\n---\n\n", 
            orderedChunks.Select(c => c.Result));
        
        return combinedMarkdown;
    }

    /// <summary>
    /// Convert DOCX using split processing by chapters for large files
    /// </summary>
    private async Task<string> ConvertDocxWithSplitProcessingAsync(string filePath, CancellationToken cancellationToken)
    {
        // Get chapter boundaries from the DOCX
        List<DocxChapter> chapters;
        try
        {
            chapters = GetDocxChapters(filePath);
            Console.WriteLine($"DOCX has {chapters.Count} chapters/sections");
            Console.Out.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not read DOCX structure: {ex.Message}");
            Console.WriteLine("Falling back to standard conversion...");
            return await ConvertStandardAsync(filePath, cancellationToken);
        }

        // For small DOCX (few chapters), just convert the whole thing
        if (chapters.Count <= 3)
        {
            Console.WriteLine("Small DOCX - using standard conversion...");
            return await ConvertStandardAsync(filePath, cancellationToken);
        }

        // Create temp files for each chapter and convert separately
        var tempDir = Path.Combine(Path.GetTempPath(), $"docsummarizer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var maxConcurrent = _config.MaxConcurrentChunks;
            Console.WriteLine($"Split processing: {chapters.Count} chapters (max {maxConcurrent} concurrent)");
            Console.Out.Flush();
            
            var chunkTasks = new List<DocxChunkTask>();
            var startTime = DateTime.UtcNow;
            
            // Create temp DOCX files for each chapter
            for (int i = 0; i < chapters.Count; i++)
            {
                var chapter = chapters[i];
                var tempPath = Path.Combine(tempDir, $"chapter_{i:D3}.docx");
                CreateDocxFromChapter(filePath, chapter, tempPath);
                chunkTasks.Add(new DocxChunkTask(i, chapter.Title, tempPath));
            }
            
            // Process in waves
            for (int waveStart = 0; waveStart < chunkTasks.Count; waveStart += maxConcurrent)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var waveChunks = chunkTasks.Skip(waveStart).Take(maxConcurrent).ToList();
                
                // Submit this wave
                Console.Write($"Wave {waveStart / maxConcurrent + 1}: ");
                foreach (var chunk in waveChunks)
                {
                    try
                    {
                        chunk.TaskId = await StartConversionAsync(chunk.TempPath, null, null, cancellationToken);
                        var shortTitle = chunk.Title.Length > 20 ? chunk.Title[..20] + "..." : chunk.Title;
                        Console.Write($"[{chunk.Index}:{shortTitle}]");
                        Console.Out.Flush();
                    }
                    catch
                    {
                        chunk.IsFailed = true;
                        Console.Write($"[{chunk.Index}:submit-fail]");
                        Console.Out.Flush();
                    }
                }
                
                // Poll for this wave to complete
                var pendingChunks = waveChunks.Where(c => !string.IsNullOrEmpty(c.TaskId) && !c.IsFailed).ToList();
                while (pendingChunks.Any())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var elapsed = DateTime.UtcNow - startTime;
                    if (elapsed > _timeout)
                    {
                        throw new TimeoutException($"Split processing timed out after {_timeout.TotalMinutes:F0} minutes");
                    }
                    
                    await Task.Delay(_pollInterval, cancellationToken);
                    
                    foreach (var chunk in pendingChunks.ToList())
                    {
                        var status = await CheckTaskStatusAsync(chunk.TaskId, cancellationToken);
                        
                        if (status == "SUCCESS")
                        {
                            chunk.IsComplete = true;
                            chunk.Result = await GetResultAsync(chunk.TaskId, cancellationToken);
                            Console.Write($"[{chunk.Index}:ok]");
                            Console.Out.Flush();
                            pendingChunks.Remove(chunk);
                        }
                        else if (status == "FAILURE" || status == "REVOKED")
                        {
                            chunk.IsFailed = true;
                            Console.Write($"[{chunk.Index}:fail]");
                            Console.Out.Flush();
                            pendingChunks.Remove(chunk);
                        }
                    }
                    
                    if (pendingChunks.Any())
                    {
                        Console.Write(".");
                        Console.Out.Flush();
                    }
                }
                
                Console.WriteLine();
            }
            
            var totalElapsed = DateTime.UtcNow - startTime;
            var successCount = chunkTasks.Count(c => c.IsComplete);
            Console.WriteLine($"Conversion complete: {successCount}/{chapters.Count} chapters in {totalElapsed.TotalSeconds:F0}s");
            
            // Concatenate successful results in order
            var orderedChunks = chunkTasks
                .Where(c => c.IsComplete && !string.IsNullOrEmpty(c.Result))
                .OrderBy(c => c.Index)
                .ToList();
            
            if (orderedChunks.Count == 0)
            {
                throw new Exception("No chapters were successfully converted");
            }
            
            // Combine with chapter titles as markdown headers
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
            // Clean up temp directory
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Extract chapter boundaries from a DOCX file based on Heading1/Heading2 styles
    /// </summary>
    private static List<DocxChapter> GetDocxChapters(string filePath)
    {
        var chapters = new List<DocxChapter>();
        
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return chapters;
        
        var elements = body.Elements().ToList();
        var currentChapterStart = 0;
        string? currentTitle = null;
        
        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i] is Paragraph para)
            {
                var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                
                // Check for Heading1, Heading2, or Title styles (chapter boundaries)
                if (styleId != null && (styleId.StartsWith("Heading1", StringComparison.OrdinalIgnoreCase) ||
                                        styleId.Equals("Title", StringComparison.OrdinalIgnoreCase) ||
                                        styleId.StartsWith("Heading2", StringComparison.OrdinalIgnoreCase)))
                {
                    // Save previous chapter if exists
                    if (currentTitle != null && i > currentChapterStart)
                    {
                        chapters.Add(new DocxChapter(currentTitle, currentChapterStart, i - 1));
                    }
                    
                    // Start new chapter
                    currentTitle = GetParagraphText(para);
                    if (string.IsNullOrWhiteSpace(currentTitle))
                        currentTitle = $"Section {chapters.Count + 1}";
                    currentChapterStart = i;
                }
            }
        }
        
        // Add final chapter
        if (currentTitle != null)
        {
            chapters.Add(new DocxChapter(currentTitle, currentChapterStart, elements.Count - 1));
        }
        else if (elements.Count > 0)
        {
            // No headings found - treat whole doc as one chapter
            chapters.Add(new DocxChapter("Document", 0, elements.Count - 1));
        }
        
        return chapters;
    }

    private static string GetParagraphText(Paragraph para)
    {
        var sb = new StringBuilder();
        foreach (var run in para.Elements<Run>())
        {
            foreach (var text in run.Elements<Text>())
            {
                sb.Append(text.Text);
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Create a new DOCX file containing only the specified chapter
    /// </summary>
    private static void CreateDocxFromChapter(string sourcePath, DocxChapter chapter, string destPath)
    {
        // Copy the source file
        File.Copy(sourcePath, destPath, overwrite: true);
        
        using var doc = WordprocessingDocument.Open(destPath, true);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return;
        
        var elements = body.Elements().ToList();
        
        // Remove elements outside the chapter range (in reverse order to preserve indices)
        for (int i = elements.Count - 1; i >= 0; i--)
        {
            if (i < chapter.StartIndex || i > chapter.EndIndex)
            {
                elements[i].Remove();
            }
        }
        
        doc.MainDocumentPart?.Document?.Save();
    }

    private record DocxChapter(string Title, int StartIndex, int EndIndex);
    
    private class DocxChunkTask
    {
        public int Index { get; }
        public string Title { get; }
        public string TempPath { get; }
        public string TaskId { get; set; } = "";
        public bool IsComplete { get; set; }
        public bool IsFailed { get; set; }
        public string? Result { get; set; }
        
        public DocxChunkTask(int index, string title, string tempPath)
        {
            Index = index;
            Title = title;
            TempPath = tempPath;
        }
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
        catch
        {
            return null;
        }
    }

    private async Task<string> StartConversionAsync(string filePath, int? startPage, int? endPage, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(filePath);
        var streamContent = new StreamContent(stream);
        content.Add(streamContent, "files", Path.GetFileName(filePath));
        
        // Use configured PDF backend
        if (filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(_config.PdfBackend))
        {
            content.Add(new StringContent(_config.PdfBackend), "pdf_backend");
        }
        
        // Add page range if specified - multipart form sends array elements with same name
        if (startPage.HasValue && endPage.HasValue)
        {
            content.Add(new StringContent(startPage.Value.ToString()), "page_range");
            content.Add(new StringContent(endPage.Value.ToString()), "page_range");
        }

        // Use the async endpoint which returns a task ID immediately
        var response = await _http.PostAsync($"{_baseUrl}/v1/convert/file/async", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var taskResponse = JsonSerializer.Deserialize(json, DocSummarizerJsonContext.Default.DoclingTaskResponse);
        
        return taskResponse?.TaskId ?? throw new Exception("No task ID returned from Docling");
    }
    
    private class PdfChunkTask
    {
        public int Index { get; }
        public int StartPage { get; }
        public int EndPage { get; }
        public string TaskId { get; set; }
        public bool IsComplete { get; set; }
        public bool IsFailed { get; set; }
        public string? Result { get; set; }
        
        public PdfChunkTask(int index, int startPage, int endPage, string taskId)
        {
            Index = index;
            StartPage = startPage;
            EndPage = endPage;
            TaskId = taskId;
        }
    }

    private async Task<string> WaitForCompletionAsync(string taskId, string label, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        var startTime = DateTime.UtcNow;
        var pollCount = 0;
        var lastStatus = "";

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            try
            {
                pollCount++;
                var elapsed = DateTime.UtcNow - startTime;
                
                // Poll for status
                var statusResponse = await _http.GetAsync($"{_baseUrl}/v1/status/poll/{taskId}", timeoutCts.Token);
                
                if (!statusResponse.IsSuccessStatusCode)
                {
                    Console.Write(".");
                    Console.Out.Flush();
                    await Task.Delay(_pollInterval, timeoutCts.Token);
                    continue;
                }

                var statusJson = await statusResponse.Content.ReadAsStringAsync(timeoutCts.Token);
                var status = JsonSerializer.Deserialize(statusJson, DocSummarizerJsonContext.Default.DoclingStatusResponse);
                var taskStatus = status?.TaskStatus?.ToUpperInvariant();

                if (taskStatus == "SUCCESS")
                {
                    Console.WriteLine($" done ({elapsed.TotalSeconds:F0}s)");
                    // Get the result
                    return await GetResultAsync(taskId, timeoutCts.Token);
                }
                else if (taskStatus == "FAILURE" || taskStatus == "REVOKED")
                {
                    Console.WriteLine(" FAILED");
                    throw new Exception($"Docling conversion failed: {status?.TaskStatus}");
                }

                // Show progress every 5 polls (10 seconds) or on status change
                if (taskStatus != lastStatus)
                {
                    lastStatus = taskStatus ?? "";
                    Console.Write($"[{lastStatus}]");
                    Console.Out.Flush();
                }
                else if (pollCount % 5 == 0)
                {
                    Console.Write($"({elapsed.TotalSeconds:F0}s)");
                    Console.Out.Flush();
                }
                else
                {
                    Console.Write(".");
                    Console.Out.Flush();
                }

                // Still processing, wait and poll again
                await Task.Delay(_pollInterval, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine(" TIMEOUT");
                throw new TimeoutException($"Document conversion timed out after {_timeout.TotalMinutes:F0} minutes");
            }
        }

        Console.WriteLine(" TIMEOUT");
        throw new TimeoutException($"Document conversion timed out after {_timeout.TotalMinutes:F0} minutes");
    }

    private async Task<string> GetResultAsync(string taskId, CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync($"{_baseUrl}/v1/result/{taskId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize(json, DocSummarizerJsonContext.Default.DoclingResultResponse);
        
        // The result contains a document with md_content
        return result?.Document?.MdContent ?? 
            throw new Exception("No markdown content returned from Docling");
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _http.Dispose();
}

// Response models for Docling API
public class DoclingTaskResponse
{
    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }
}

public class DoclingStatusResponse
{
    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }
    
    [JsonPropertyName("task_status")]
    public string? TaskStatus { get; set; }
}

public class DoclingResultResponse
{
    [JsonPropertyName("document")]
    public DoclingDocument? Document { get; set; }
}

public class DoclingDocument
{
    [JsonPropertyName("md_content")]
    public string? MdContent { get; set; }
}

// Legacy response class for backwards compatibility
public class DoclingResponse
{
    [JsonPropertyName("document")]
    public DoclingDocument? Document { get; set; }
}
