using System.Collections.Concurrent;
using Mostlylucid.SemanticGallery.Demo.Models;
using Mostlylucid.SemanticSearch.Services;

namespace Mostlylucid.SemanticGallery.Demo.Services;

/// <summary>
/// In-memory gallery service for demo purposes
/// In production, use QdrantGalleryService
/// </summary>
public class InMemoryGalleryService
{
    private readonly ILogger<InMemoryGalleryService> _logger;
    private readonly IEmbeddingService? _embeddingService;
    private readonly ConcurrentDictionary<Guid, GalleryImage> _images = new();
    private readonly ConcurrentDictionary<Guid, (GalleryImage Image, float[] Embedding)> _imageEmbeddings = new();

    public InMemoryGalleryService(
        ILogger<InMemoryGalleryService> logger,
        IEmbeddingService? embeddingService = null)
    {
        _logger = logger;
        _embeddingService = embeddingService;
    }

    public async Task IndexImageAsync(GalleryImage image, string caption)
    {
        try
        {
            // Store the image
            _images[image.Id] = image;

            // Generate embedding if service is available
            if (_embeddingService != null)
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(caption);
                _imageEmbeddings[image.Id] = (image, embedding);
                _logger.LogInformation("Indexed image {ImageId} with semantic embedding", image.Id);
            }
            else
            {
                _logger.LogWarning("Embedding service not available, indexing without semantic search");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing image");
        }
    }

    public async Task<List<SearchResult>> SemanticSearchAsync(string query, int limit = 20)
    {
        try
        {
            if (_embeddingService == null || !_imageEmbeddings.Any())
            {
                _logger.LogWarning("Semantic search not available, returning all images");
                return _images.Values
                    .Select(img => new SearchResult
                    {
                        Image = img,
                        Score = 1.0f,
                        MatchReason = "Listing all images (semantic search unavailable)"
                    })
                    .Take(limit)
                    .ToList();
            }

            // Generate query embedding
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

            // Calculate similarity with all images
            var results = new List<(GalleryImage Image, float Score)>();

            foreach (var (imageId, (image, imageEmbedding)) in _imageEmbeddings)
            {
                var similarity = CosineSimilarity(queryEmbedding, imageEmbedding);
                if (similarity > 0.3f) // Threshold
                {
                    results.Add((image, similarity));
                }
            }

            // Sort by similarity and return top results
            return results
                .OrderByDescending(r => r.Score)
                .Take(limit)
                .Select(r => new SearchResult
                {
                    Image = r.Image,
                    Score = r.Score,
                    MatchReason = $"Semantic match: {(r.Score * 100):F0}% similarity"
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing semantic search");
            return new List<SearchResult>();
        }
    }

    public Task<List<SearchResult>> SearchByPersonAsync(string personName, int limit = 50)
    {
        // Find images with this person
        var results = _images.Values
            .Where(img => img.Faces.Any(f => f.PersonName?.Equals(personName, StringComparison.OrdinalIgnoreCase) == true))
            .Select(img => new SearchResult
            {
                Image = img,
                Score = 1.0f,
                MatchReason = $"Contains {personName}"
            })
            .Take(limit)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<GalleryImage>> GetAllImagesAsync()
    {
        return Task.FromResult(_images.Values.OrderByDescending(img => img.UploadedAt).ToList());
    }

    public Task<GalleryImage?> GetImageByIdAsync(Guid imageId)
    {
        _images.TryGetValue(imageId, out var image);
        return Task.FromResult(image);
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0f;

        float dotProduct = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0f || normB == 0f)
            return 0f;

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
}
