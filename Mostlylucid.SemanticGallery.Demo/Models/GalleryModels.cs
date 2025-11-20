namespace Mostlylucid.SemanticGallery.Demo.Models;

/// <summary>
/// Represents an image in the gallery
/// </summary>
public class GalleryImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // AI-generated metadata
    public string Caption { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public List<string> DetectedObjects { get; set; } = new();

    // Faces found in this image
    public List<DetectedFace> Faces { get; set; } = new();

    // User metadata
    public List<string> Tags { get; set; } = new();
    public string? UserDescription { get; set; }
}

/// <summary>
/// Represents a detected face in an image
/// </summary>
public class DetectedFace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImageId { get; set; }

    // Bounding box (normalized 0-1)
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    // Face embedding for recognition
    public float[] Embedding { get; set; } = Array.Empty<float>();

    // Recognition result
    public Guid? PersonId { get; set; }
    public string? PersonName { get; set; }
    public float Confidence { get; set; }
}

/// <summary>
/// Represents a person with known faces
/// </summary>
public class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Representative face embeddings
    public List<float[]> FaceEmbeddings { get; set; } = new();

    // Statistics
    public int PhotoCount { get; set; }
    public DateTime? LastSeenDate { get; set; }
}

/// <summary>
/// Search result with relevance score
/// </summary>
public class SearchResult
{
    public GalleryImage Image { get; set; } = null!;
    public float Score { get; set; }
    public string MatchReason { get; set; } = string.Empty;
    public DetectedFace? MatchedFace { get; set; }
}

public class CreatePersonRequest
{
    public string Name { get; set; } = string.Empty;
    public List<Guid> FaceIds { get; set; } = new();
}
