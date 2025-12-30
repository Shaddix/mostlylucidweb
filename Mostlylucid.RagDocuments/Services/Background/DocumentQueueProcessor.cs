using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mostlylucid.DocSummarizer;
using Mostlylucid.DocSummarizer.Services;
using Mostlylucid.RagDocuments.Config;
using Mostlylucid.RagDocuments.Data;
using Mostlylucid.RagDocuments.Entities;

namespace Mostlylucid.RagDocuments.Services.Background;

public class DocumentQueueProcessor(
    DocumentProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<DocumentQueueProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Document queue processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await queue.DequeueAsync(stoppingToken);
                await ProcessDocumentAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing document from queue");
            }
        }

        logger.LogInformation("Document queue processor stopped");
    }

    private async Task ProcessDocumentAsync(DocumentProcessingJob job, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RagDocumentsDbContext>();
        var summarizer = scope.ServiceProvider.GetRequiredService<IDocumentSummarizer>();

        var document = await db.Documents.FindAsync([job.DocumentId], ct);
        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} not found, skipping", job.DocumentId);
            return;
        }

        var progressChannel = queue.GetOrCreateProgressChannel(job.DocumentId);

        try
        {
            document.Status = DocumentStatus.Processing;
            document.ProcessingProgress = 0;
            await db.SaveChangesAsync(ct);

            // Report start
            await progressChannel.Writer.WriteAsync(
                ProgressUpdates.Stage("Processing", "Starting document processing...", 0, 0), ct);

            // Process document using DocSummarizer
            var result = await summarizer.SummarizeFileAsync(
                job.FilePath,
                progressChannel.Writer,
                cancellationToken: ct);

            // Update document with results
            document.Status = DocumentStatus.Completed;
            document.ProcessingProgress = 100;
            document.SegmentCount = result.Trace.TotalChunks;
            document.ProcessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            // Report completion
            await progressChannel.Writer.WriteAsync(
                ProgressUpdates.Completed($"Completed! {document.SegmentCount} segments extracted.", 0), ct);

            logger.LogInformation("Document {DocumentId} processed successfully with {SegmentCount} segments",
                job.DocumentId, document.SegmentCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document {DocumentId}", job.DocumentId);

            document.Status = DocumentStatus.Failed;
            document.StatusMessage = ex.Message;
            await db.SaveChangesAsync(ct);

            await progressChannel.Writer.WriteAsync(
                ProgressUpdates.Error("Processing", $"Failed: {ex.Message}", 0), ct);
        }
        finally
        {
            queue.CompleteProgressChannel(job.DocumentId);
        }
    }
}
