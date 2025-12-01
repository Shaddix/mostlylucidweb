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
    private const int DefaultFeedLimit = 20;
    private const int DefaultTtlMinutes = 60; // Suggest checking every hour
    private const string AuthorName = "Scott Galloway";
    private const string AuthorEmail = "scott@mostlylucid.net";
    private const string Copyright = "Copyright 2024-2025 Scott Galloway. All rights reserved.";

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

    /// <summary>
    /// Maps language codes to RFC 5646/ISO 639 language tags for RSS
    /// </summary>
    private static string GetRssLanguageCode(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "en" => "en-gb",
            "es" => "es",
            "fr" => "fr",
            "de" => "de",
            "it" => "it",
            "nl" => "nl",
            "pt" => "pt",
            "pl" => "pl",
            "sv" => "sv",
            "fi" => "fi",
            "ar" => "ar",
            "el" => "el",
            "hi" => "hi",
            "uk" => "uk",
            "zh" => "zh-cn",
            _ => language
        };
    }

    public async Task<string> GenerateFeed(string? language = null, int? limit = null)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? MarkdownBaseService.EnglishLanguage : language;
        var allItems = await blogViewService.GetPostsForLanguage(null, null, lang);
        var totalPosts = allItems.Count;
        var feedLimit = limit ?? DefaultFeedLimit;
        var items = allItems.OrderByDescending(x => x.PublishedDate).Take(feedLimit).ToList();

        List<RssFeedItem> rssFeedItems = new();
        foreach (var item in items)
        {
            rssFeedItems.Add(new RssFeedItem
            {
                Title = item.Title,
                Link = $"{GetSiteUrl()}{BlogUrlHelper.GetBlogUrl(item.Slug, lang)}",
                Summary = item.Summary,
                PubDate = item.PublishedDate,
                Categories = item.Categories,
                Slug = item.Slug,
                Author = AuthorName
            });
        }

        var lastBuildDate = items.Count > 0 ? items.Max(x => x.PublishedDate) : DateTime.UtcNow;
        return GenerateFeed(rssFeedItems, lang, totalPosts, feedLimit, lastBuildDate);
    }

    private string GenerateFeed(IEnumerable<RssFeedItem> items, string language, int totalPosts, int feedLimit, DateTime lastBuildDate)
    {
        XNamespace atom = "http://www.w3.org/2005/Atom";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";

        var siteUrl = GetSiteUrl();
        var isEnglish = string.IsNullOrEmpty(language) || language.Equals(MarkdownBaseService.EnglishLanguage, StringComparison.OrdinalIgnoreCase);
        var feedUrl = isEnglish ? $"{siteUrl}/rss" : $"{siteUrl}/rss/{language}";
        var title = isEnglish ? "mostlylucid.net" : $"mostlylucid.net ({language})";
        var rssLanguage = GetRssLanguageCode(language);

        var hasMore = totalPosts > feedLimit;
        var description = hasMore
            ? $"The {feedLimit} most recent posts from mostlylucid.net ({totalPosts} total). Visit {siteUrl}/blog for the full archive."
            : "The latest posts from mostlylucid.net - a blog about software development, .NET, and technology.";

        var feed = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "atom", atom.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
                new XElement("channel",
                    // Required elements
                    new XElement("title", title),
                    new XElement("link", siteUrl),
                    new XElement("description", description),

                    // Recommended channel elements
                    new XElement("language", rssLanguage),
                    new XElement("lastBuildDate", lastBuildDate.ToUniversalTime().ToString("R")),
                    new XElement("pubDate", lastBuildDate.ToUniversalTime().ToString("R")),
                    new XElement("ttl", DefaultTtlMinutes),
                    new XElement("generator", "mostlylucid.net RSS Generator"),
                    new XElement("copyright", Copyright),
                    new XElement("managingEditor", $"{AuthorEmail} ({AuthorName})"),
                    new XElement("webMaster", $"{AuthorEmail} ({AuthorName})"),

                    // Atom self-link (required for valid Atom compatibility)
                    new XElement(atom + "link",
                        new XAttribute("href", feedUrl),
                        new XAttribute("rel", "self"),
                        new XAttribute("type", "application/rss+xml")),

                    // Optional: Image/logo
                    new XElement("image",
                        new XElement("url", $"{siteUrl}/img/favicon.png"),
                        new XElement("title", title),
                        new XElement("link", siteUrl),
                        new XElement("width", "32"),
                        new XElement("height", "32")),

                    // Items
                    from item in items
                    select new XElement("item",
                        new XElement("title", item.Title),
                        new XElement("link", item.Link),
                        new XElement("guid", item.Guid, new XAttribute("isPermaLink", "false")),
                        new XElement("description", new XCData(item.Description)),
                        new XElement("pubDate", item.PubDate.ToUniversalTime().ToString("R")),
                        new XElement(dc + "creator", item.Author),
                        from category in item.Categories
                        select new XElement("category", category)
                    )
                )
            )
        );

        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(false), // UTF-8 without BOM
            OmitXmlDeclaration = false
        };

        using var memoryStream = new MemoryStream();
        using var writer = XmlWriter.Create(memoryStream, settings);
        feed.Save(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
}