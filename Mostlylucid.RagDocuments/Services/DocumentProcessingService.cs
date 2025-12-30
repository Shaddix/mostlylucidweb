using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mostlylucid.DocSummarizer.Services;
using Mostlylucid.RagDocuments.Config;
using Mostlylucid.RagDocuments.Data;
using Mostlylucid.RagDocuments.Entities;
using Mostlylucid.RagDocuments.Services.Background;

namespace Mostlylucid.RagDocuments.Services;

public class DocumentProcessingService(
    RagDocumentsDbContext db,
    DocumentProcessingQueue queue,
    IOptions<RagDocumentsConfig> config,
    ILogger<DocumentProcessingService> logger) : IDocumentProcessingService
{
    private readonly RagDocumentsConfig _config = config.Value;

    public async Task<Guid> QueueDocumentAsync(Stream fileStream, string filename, Guid? collectionId, CancellationToken ct = default)
    {
        // Validate extension
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        if (!_config.AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException($"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _config.AllowedExtensions)}");
        }

        // Compute content hash
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(fileStream, ct);
        var contentHash = Convert.ToHexString(hashBytes[..16]).ToLowerInvariant();
        fileStream.Position = 0;

        // Check for duplicate
        var existingDoc = await db.Documents
            .FirstOrDefaultAsync(d => d.ContentHash == contentHash && d.CollectionId == collectionId, ct);
        if (existingDoc is not null)
        {
            logger.LogInformation("Document with hash {Hash} already exists as {DocumentId}", contentHash, existingDoc.Id);
            return existingDoc.Id;
        }

        // Save file
        var documentId = Guid.NewGuid();
        var uploadDir = Path.Combine(_config.UploadPath, documentId.ToString());
        Directory.CreateDirectory(uploadDir);
        var filePath = Path.Combine(uploadDir, filename);

        await using (var fs = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(fs, ct);
        }

        // Create document entity
        var document = new DocumentEntity
        {
            Id = documentId,
            CollectionId = collectionId,
            Name = Path.GetFileNameWithoutExtension(filename),
            OriginalFilename = filename,
            ContentHash = contentHash,
            FilePath = filePath,
            FileSizeBytes = new FileInfo(filePath).Length,
            MimeType = GetMimeType(extension),
            Status = DocumentStatus.Pending
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);

        // Queue for processing
        await queue.EnqueueAsync(new DocumentProcessingJob(documentId, filePath, collectionId), ct);

        logger.LogInformation("Document {DocumentId} queued for processing: {Filename}", documentId, filename);

        return documentId;
    }

    public async Task<DocumentEntity?> GetDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        return await db.Documents
            .Include(d => d.Collection)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);
    }

    public async Task<List<DocumentEntity>> GetDocumentsAsync(Guid? collectionId = null, CancellationToken ct = default)
    {
        var query = db.Documents.Include(d => d.Collection).AsQueryable();

        if (collectionId.HasValue)
        {
            query = query.Where(d => d.CollectionId == collectionId);
        }

        return await query.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await db.Documents.FindAsync([documentId], ct);
        if (document is null) return;

        // Delete file
        if (!string.IsNullOrEmpty(document.FilePath) && File.Exists(document.FilePath))
        {
            var dir = Path.GetDirectoryName(document.FilePath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        db.Documents.Remove(document);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Document {DocumentId} deleted", documentId);
    }

    public async IAsyncEnumerable<ProgressUpdate> StreamProgressAsync(Guid documentId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!queue.TryGetProgressChannel(documentId, out var channel) || channel is null)
        {
            // Document might already be processed or doesn't exist
            var doc = await GetDocumentAsync(documentId, ct);
            if (doc is null)
            {
                yield return ProgressUpdates.Error("Status", "Document not found", 0);
                yield break;
            }

            yield return doc.Status switch
            {
                DocumentStatus.Completed => ProgressUpdates.Completed($"Completed with {doc.SegmentCount} segments", 0),
                DocumentStatus.Failed => ProgressUpdates.Error("Processing", doc.StatusMessage ?? "Failed", 0),
                DocumentStatus.Pending => ProgressUpdates.Stage("Pending", "Waiting in queue...", 0, 0),
                _ => ProgressUpdates.Stage("Processing", $"Progress: {doc.ProcessingProgress}%", doc.ProcessingProgress, 0)
            };
            yield break;
        }

        await foreach (var update in channel.Reader.ReadAllAsync(ct))
        {
            yield return update;
        }
    }

    private static string GetMimeType(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".md" => "text/markdown",
        ".txt" => "text/plain",
        ".html" => "text/html",
        _ => "application/octet-stream"
    };
}
