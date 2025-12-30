using System.Collections.Concurrent;
using System.Threading.Channels;
using Mostlylucid.DocSummarizer.Services;

namespace Mostlylucid.RagDocuments.Services.Background;

public class DocumentProcessingQueue
{
    private readonly Channel<DocumentProcessingJob> _queue = Channel.CreateUnbounded<DocumentProcessingJob>();
    private readonly ConcurrentDictionary<Guid, Channel<ProgressUpdate>> _progressChannels = new();

    public async ValueTask EnqueueAsync(DocumentProcessingJob job, CancellationToken ct = default)
    {
        await _queue.Writer.WriteAsync(job, ct);
    }

    public async ValueTask<DocumentProcessingJob> DequeueAsync(CancellationToken ct = default)
    {
        return await _queue.Reader.ReadAsync(ct);
    }

    public Channel<ProgressUpdate> GetOrCreateProgressChannel(Guid documentId)
    {
        return _progressChannels.GetOrAdd(documentId, _ => Channel.CreateUnbounded<ProgressUpdate>());
    }

    public void CompleteProgressChannel(Guid documentId)
    {
        if (_progressChannels.TryRemove(documentId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public bool TryGetProgressChannel(Guid documentId, out Channel<ProgressUpdate>? channel)
    {
        return _progressChannels.TryGetValue(documentId, out channel);
    }
}

public record DocumentProcessingJob(
    Guid DocumentId,
    string FilePath,
    Guid? CollectionId);
