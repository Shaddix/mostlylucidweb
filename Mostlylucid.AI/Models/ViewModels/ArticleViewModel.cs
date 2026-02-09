namespace Mostlylucid.AI.Models.ViewModels;

public class ArticleViewModel : AIBaseViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public DateTime PublishedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public string[] Categories { get; set; } = [];
    public int WordCount { get; set; }
    public string Language { get; set; } = "en";
    public string[] Languages { get; set; } = [];

    public string ReadingTime
    {
        get
        {
            var minutes = (int)Math.Ceiling(WordCount / 200.0);
            return $"{minutes} min read";
        }
    }
}
