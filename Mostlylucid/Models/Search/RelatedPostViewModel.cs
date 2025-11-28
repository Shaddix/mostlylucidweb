namespace Mostlylucid.Models.Search;

/// <summary>
/// View model for related posts - combines semantic score with PostgreSQL details
/// </summary>
public class RelatedPostViewModel
{
    public required string Slug { get; set; }
    public required string Title { get; set; }
    public DateTime PublishedDate { get; set; }
    public string[] Categories { get; set; } = Array.Empty<string>();
    public float Score { get; set; }
}
