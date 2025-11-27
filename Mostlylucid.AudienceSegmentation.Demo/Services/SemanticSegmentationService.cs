using Microsoft.Extensions.Logging;
using Mostlylucid.AudienceSegmentation.Demo.Models;
using Mostlylucid.SemanticSearch.Services;

namespace Mostlylucid.AudienceSegmentation.Demo.Services;

/// <summary>
/// Performs semantic-based audience segmentation using embeddings and clustering
/// </summary>
public class SemanticSegmentationService
{
    private readonly ILogger<SemanticSegmentationService> _logger;
    private readonly IEmbeddingService _embeddingService;
    private List<CustomerSegment> _segments = new();

    public SemanticSegmentationService(
        ILogger<SemanticSegmentationService> logger,
        IEmbeddingService embeddingService)
    {
        _logger = logger;
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Generate embeddings for all products
    /// </summary>
    public async Task<List<Product>> EnrichProductsWithEmbeddingsAsync(
        List<Product> products,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating embeddings for {Count} products...", products.Count);

        foreach (var product in products)
        {
            // Create rich text representation for embedding
            var text = $"{product.Name}. {product.Description}. " +
                      $"Category: {product.Category}. " +
                      $"Tags: {string.Join(", ", product.Tags)}. " +
                      $"Target: {product.TargetAudience}";

            product.Embedding = await _embeddingService.GenerateEmbeddingAsync(text, cancellationToken);
        }

        _logger.LogInformation("Product embeddings generated successfully");
        return products;
    }

    /// <summary>
    /// Perform K-means clustering on customer embeddings to discover segments
    /// </summary>
    public async Task<List<CustomerSegment>> DiscoverSegmentsAsync(
        List<Customer> customers,
        List<Product> products,
        int numberOfSegments = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Discovering {Count} customer segments using K-means clustering...",
            numberOfSegments);

        // Filter customers with embeddings
        var customersWithEmbeddings = customers
            .Where(c => c.ProfileEmbedding != null && c.ProfileEmbedding.Length > 0)
            .ToList();

        if (customersWithEmbeddings.Count < numberOfSegments)
        {
            _logger.LogWarning("Not enough customers ({Count}) for {Segments} segments",
                customersWithEmbeddings.Count, numberOfSegments);
            return new List<CustomerSegment>();
        }

        // Initialize K-means centroids randomly
        var centroids = new List<float[]>();
        var random = new Random();
        var selectedIndices = new HashSet<int>();

        while (centroids.Count < numberOfSegments)
        {
            var index = random.Next(customersWithEmbeddings.Count);
            if (selectedIndices.Add(index))
            {
                centroids.Add((float[])customersWithEmbeddings[index].ProfileEmbedding!.Clone());
            }
        }

        // K-means iterations
        var maxIterations = 50;
        var assignments = new int[customersWithEmbeddings.Count];

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            var changed = false;

            // Assignment step: assign each customer to nearest centroid
            for (int i = 0; i < customersWithEmbeddings.Count; i++)
            {
                var customer = customersWithEmbeddings[i];
                var nearest = FindNearestCentroid(customer.ProfileEmbedding!, centroids);

                if (assignments[i] != nearest)
                {
                    assignments[i] = nearest;
                    changed = true;
                }
            }

            if (!changed)
            {
                _logger.LogInformation("K-means converged after {Iterations} iterations", iteration + 1);
                break;
            }

            // Update step: recalculate centroids
            for (int k = 0; k < numberOfSegments; k++)
            {
                var membersOfCluster = customersWithEmbeddings
                    .Where((_, idx) => assignments[idx] == k)
                    .ToList();

                if (membersOfCluster.Any())
                {
                    centroids[k] = CalculateCentroid(
                        membersOfCluster.Select(c => c.ProfileEmbedding!).ToList()
                    );
                }
            }
        }

        // Create segments from clusters
        _segments = new List<CustomerSegment>();

        for (int k = 0; k < numberOfSegments; k++)
        {
            var membersOfSegment = customersWithEmbeddings
                .Where((_, idx) => assignments[idx] == k)
                .ToList();

            if (!membersOfSegment.Any()) continue;

            var segment = new CustomerSegment
            {
                Name = $"Segment {k + 1}",
                CentroidEmbedding = centroids[k],
                CustomerIds = membersOfSegment.Select(c => c.Id).ToList()
            };

            // Analyze segment characteristics
            await AnalyzeSegmentCharacteristicsAsync(segment, membersOfSegment, products, cancellationToken);

            _segments.Add(segment);

            // Update customers with segment assignment
            foreach (var customer in membersOfSegment)
            {
                customer.CurrentSegment = segment.Id;
            }
        }

        _logger.LogInformation("Discovered {Count} segments", _segments.Count);
        return _segments;
    }

    /// <summary>
    /// Assign a customer to the most appropriate segment
    /// </summary>
    public (CustomerSegment segment, double confidence) AssignToSegment(Customer customer)
    {
        if (customer.ProfileEmbedding == null || !_segments.Any())
        {
            throw new InvalidOperationException("Customer embedding or segments not available");
        }

        // Find segment with closest centroid
        var scores = new Dictionary<CustomerSegment, double>();

        foreach (var segment in _segments)
        {
            if (segment.CentroidEmbedding != null)
            {
                var similarity = CosineSimilarity(customer.ProfileEmbedding, segment.CentroidEmbedding);
                scores[segment] = similarity;
            }
        }

        var bestSegment = scores.OrderByDescending(kvp => kvp.Value).First();
        customer.CurrentSegment = bestSegment.Key.Id;

        return (bestSegment.Key, bestSegment.Value);
    }

    /// <summary>
    /// Get product recommendations for a segment
    /// </summary>
    public List<Product> GetSegmentRecommendations(
        CustomerSegment segment,
        List<Product> allProducts,
        int topN = 10)
    {
        if (segment.CentroidEmbedding == null)
            return new List<Product>();

        // Score products based on similarity to segment centroid
        var scoredProducts = allProducts
            .Where(p => p.Embedding != null)
            .Select(p => new
            {
                Product = p,
                Score = CosineSimilarity(segment.CentroidEmbedding, p.Embedding!)
            })
            .OrderByDescending(x => x.Score)
            .Take(topN)
            .Select(x => x.Product)
            .ToList();

        return scoredProducts;
    }

    private async Task AnalyzeSegmentCharacteristicsAsync(
        CustomerSegment segment,
        List<Customer> members,
        List<Product> products,
        CancellationToken cancellationToken)
    {
        // Aggregate category interests across segment
        var categoryInterests = new Dictionary<string, int>();

        foreach (var member in members)
        {
            foreach (var (category, interest) in member.CategoryInterests)
            {
                categoryInterests[category] = categoryInterests.GetValueOrDefault(category) + interest;
            }
        }

        // Top categories become characteristic keywords
        segment.CharacteristicKeywords = categoryInterests
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => kvp.Key)
            .ToList();

        // Calculate average customer value (total purchases)
        var totalPurchases = members.Sum(m => m.PurchasedProducts.Count);
        segment.AverageCustomerValue = totalPurchases * 50m; // Simplified calculation

        // Find recommended products
        segment.RecommendedProductIds = GetSegmentRecommendations(segment, products, 10)
            .Select(p => p.Id)
            .ToList();

        // Generate segment description using LLM (simplified for demo)
        segment.Description = $"Segment interested in {string.Join(", ", segment.CharacteristicKeywords)}. " +
                             $"{segment.Size} customers with average value ${segment.AverageCustomerValue:F2}";
    }

    private int FindNearestCentroid(float[] embedding, List<float[]> centroids)
    {
        var maxSimilarity = double.MinValue;
        var nearestIndex = 0;

        for (int i = 0; i < centroids.Count; i++)
        {
            var similarity = CosineSimilarity(embedding, centroids[i]);
            if (similarity > maxSimilarity)
            {
                maxSimilarity = similarity;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private float[] CalculateCentroid(List<float[]> embeddings)
    {
        if (!embeddings.Any())
            return new float[384]; // Default vector size

        var dimensions = embeddings[0].Length;
        var centroid = new float[dimensions];

        for (int d = 0; d < dimensions; d++)
        {
            centroid[d] = embeddings.Average(e => e[d]);
        }

        // Normalize
        return NormalizeVector(centroid);
    }

    private float[] NormalizeVector(float[] vector)
    {
        var magnitude = (float)Math.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }
        return vector;
    }

    private double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same length");

        double dotProduct = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
        }

        return dotProduct; // Vectors are already normalized
    }

    public List<CustomerSegment> GetAllSegments() => _segments;
}
