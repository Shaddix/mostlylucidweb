namespace Mostlylucid.Services.Blog;

/// <summary>
/// Scoped service that holds the current blog post ID during markdown processing.
/// This allows the FetchMarkdownInlineParser to access the blog post ID when saving fetch metadata.
/// </summary>
public class BlogPostProcessingContext
{
    /// <summary>
    /// The ID of the blog post currently being processed.
    /// Set to 0 when no blog post is being processed.
    /// </summary>
    public int CurrentBlogPostId { get; set; }

    /// <summary>
    /// The slug of the blog post currently being processed.
    /// Useful for debugging and logging.
    /// </summary>
    public string? CurrentSlug { get; set; }

    /// <summary>
    /// Sets the current blog post context
    /// </summary>
    public void SetContext(int blogPostId, string? slug = null)
    {
        CurrentBlogPostId = blogPostId;
        CurrentSlug = slug;
    }

    /// <summary>
    /// Clears the current blog post context
    /// </summary>
    public void Clear()
    {
        CurrentBlogPostId = 0;
        CurrentSlug = null;
    }
}
