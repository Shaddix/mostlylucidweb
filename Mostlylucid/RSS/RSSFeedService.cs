using System.Text;
using System.Xml;
using System.Xml.Linq;
using Mostlylucid.Blog.ViewServices;
using Mostlylucid.Helpers;
using Mostlylucid.RSS.Models;
using Mostlylucid.Services.Markdown;

namespace Mostlylucid.RSS;

public class RSSFeedService(IBlogViewService blogViewService, IHttpContextAccessor httpContextAccessor, ILogger<RSSFeedService> logger)
{
    private string GetSiteUrl()
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            logger.LogError("Request is null");
            return string.Empty;
        }
        return $"https://{request.Host}";
    }
    
    public async Task<string> GenerateFeed(DateTime? startDate = null, string? language = null)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? MarkdownBaseService.EnglishLanguage : language;
        var items = await blogViewService.GetPostsForLanguage(startDate, null, lang);
        items = items.OrderByDescending(x => x.PublishedDate).ToList();
        List<RssFeedItem> rssFeedItems = new();
        foreach (var item in items)
        {
            rssFeedItems.Add(new RssFeedItem()
            {
                Title = item.Title,
                Link = $"{GetSiteUrl()}{BlogUrlHelper.GetBlogUrl(item.Slug, lang)}",
                Description = item.Title,
                PubDate = item.PublishedDate,
                Categories = item.Categories,
                Slug = item.Slug
            });
        }
        return GenerateFeed(rssFeedItems, lang);
    }

    private string GenerateFeed(IEnumerable<RssFeedItem> items, string language = "")
    {
        XNamespace atom = "http://www.w3.org/2005/Atom";
        var isEnglish = string.IsNullOrEmpty(language) || language.Equals(MarkdownBaseService.EnglishLanguage, StringComparison.OrdinalIgnoreCase);
        var title = isEnglish ? "mostlylucid.net" : $"mostlylucid.net ({language})";
        var feed = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss", new XAttribute(XNamespace.Xmlns + "atom", atom.NamespaceName), new XAttribute("version", "2.0"),
                new XElement("channel",
                    new XElement("title", title),
                    new XElement("link", $"{GetSiteUrl()}/rss"),
                    new XElement("description", "The latest posts from mostlylucid.net"),
                    new XElement("pubDate", DateTime.UtcNow.ToString("R")),
                    new XElement(atom + "link",
                        new XAttribute("href", $"{GetSiteUrl()}/rss"),
                        new XAttribute("rel", "self"),
                        new XAttribute("type", "application/rss+xml")),
                    from item in items
                    select new XElement("item",
                        new XElement("title", item.Title),
                        new XElement("link", item.Link),
                        new XElement("guid", item.Guid, new XAttribute("isPermaLink", "false")),
                        new XElement("description", item.Description),
                        new XElement("pubDate", item.PubDate.ToString("R")),
                        from category in item.Categories
                        select new XElement("category", category)
                    )
                )
            )
        );

        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(false) // UTF-8 without BOM
        };

        using var memoryStream = new MemoryStream();
        using var writer = XmlWriter.Create(memoryStream, settings);
        feed.Save(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
}