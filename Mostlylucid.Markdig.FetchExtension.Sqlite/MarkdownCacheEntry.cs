using System.ComponentModel.DataAnnotations;

namespace Mostlylucid.Markdig.FetchExtension.Sqlite;

/// <summary>
///     Entity for storing cached markdown in database.
/// </summary>
public class MarkdownCacheEntry
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    public int BlogPostId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset LastFetchedAt { get; set; }

    [MaxLength(128)]
    public string CacheKey { get; set; } = string.Empty;
}
