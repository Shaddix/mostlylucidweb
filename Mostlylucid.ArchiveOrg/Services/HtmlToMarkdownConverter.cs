using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.ArchiveOrg.Config;
using Mostlylucid.ArchiveOrg.Models;
using ReverseMarkdown;

namespace Mostlylucid.ArchiveOrg.Services;

public partial class HtmlToMarkdownConverter : IHtmlToMarkdownConverter
{
    private readonly MarkdownConversionOptions _options;
    private readonly ArchiveOrgOptions _archiveOptions;
    private readonly IOllamaTagGenerator _tagGenerator;
    private readonly ILogger<HtmlToMarkdownConverter> _logger;
    private readonly Converter _markdownConverter;
    private readonly HttpClient _httpClient;

    public HtmlToMarkdownConverter(
        IOptions<MarkdownConversionOptions> options,
        IOptions<ArchiveOrgOptions> archiveOptions,
        IOllamaTagGenerator tagGenerator,
        HttpClient httpClient,
        ILogger<HtmlToMarkdownConverter> logger)
    {
        _options = options.Value;
        _archiveOptions = archiveOptions.Value;
        _tagGenerator = tagGenerator;
        _httpClient = httpClient;
        _logger = logger;

        // Configure ReverseMarkdown
        _markdownConverter = new Converter(new Config
        {
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true,
            UnknownTags = Config.UnknownTagsOption.Bypass,
            TableWithoutHeaderRowHandling = Config.TableWithoutHeaderRowHandlingOption.ConvertToHtmlTable
        });
    }

    public async Task<List<MarkdownArticle>> ConvertAllAsync(
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var articles = new List<MarkdownArticle>();

        // Ensure directories exist
        Directory.CreateDirectory(_options.OutputDirectory);
        var imagesDir = Path.Combine(_options.OutputDirectory, _options.ImagesDirectory);
        Directory.CreateDirectory(imagesDir);

        // Get all HTML files
        var htmlFiles = Directory.GetFiles(_options.InputDirectory, _options.FilePattern, SearchOption.AllDirectories);
        var progressReport = new ConversionProgress { TotalFiles = htmlFiles.Length };

        _logger.LogInformation("Found {Count} HTML files to convert", htmlFiles.Length);

        foreach (var htmlFile in htmlFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progressReport.CurrentFile = htmlFile;
            progress?.Report(progressReport);

            try
            {
                var article = await ConvertFileAsync(htmlFile, cancellationToken);
                if (article != null)
                {
                    articles.Add(article);
                    progressReport.SuccessfulConversions++;
                }
                else
                {
                    progressReport.FailedConversions++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert {File}", htmlFile);
                progressReport.FailedConversions++;
            }

            progressReport.ProcessedFiles++;
            progress?.Report(progressReport);
        }

        return articles;
    }

    public async Task<MarkdownArticle?> ConvertFileAsync(
        string htmlFilePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await File.ReadAllTextAsync(htmlFilePath, cancellationToken);

            // Extract metadata from our archive comment
            var metadata = ExtractArchiveMetadata(html);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove unwanted elements
            RemoveUnwantedElements(doc);

            // Extract the main content
            var contentNode = ExtractMainContent(doc);
            if (contentNode == null)
            {
                _logger.LogWarning("Could not find main content in {File}", htmlFilePath);
                return null;
            }

            // Extract title
            var title = ExtractTitle(doc, contentNode);
            if (string.IsNullOrEmpty(title))
            {
                title = Path.GetFileNameWithoutExtension(htmlFilePath);
            }

            // Rewrite links to be blog-relative
            RewriteLinks(contentNode, metadata.originalUrl);

            // Handle images
            var images = await ProcessImagesAsync(contentNode, metadata.originalUrl, cancellationToken);

            // Convert to markdown
            var markdown = _markdownConverter.Convert(contentNode.OuterHtml);

            // Clean up the markdown
            markdown = CleanMarkdown(markdown);

            // Extract or infer publish date
            DateTime? publishDate = null;
            if (_options.ExtractDates)
            {
                publishDate = ExtractPublishDate(doc, html) ?? metadata.archiveDate;
            }

            // Generate slug
            var slug = GenerateSlug(title, metadata.originalUrl);

            // Generate tags using LLM if enabled
            List<string> categories = [];
            if (_options.GenerateTags)
            {
                categories = await _tagGenerator.GenerateTagsAsync(title, markdown, cancellationToken);
            }

            var article = new MarkdownArticle
            {
                Title = title,
                Slug = slug,
                OriginalUrl = metadata.originalUrl,
                ArchiveDate = metadata.archiveDate,
                PublishDate = publishDate,
                Categories = categories,
                MarkdownContent = markdown,
                SourceFilePath = htmlFilePath,
                Images = images
            };

            // Write the markdown file
            var outputPath = Path.Combine(_options.OutputDirectory, $"{slug}.md");
            article.OutputFilePath = outputPath;
            await File.WriteAllTextAsync(outputPath, article.ToFullMarkdown(), cancellationToken);

            _logger.LogInformation("Converted: {File} -> {Output}", htmlFilePath, outputPath);
            return article;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting {File}", htmlFilePath);
            return null;
        }
    }

    private static (string originalUrl, DateTime archiveDate) ExtractArchiveMetadata(string html)
    {
        var originalUrl = string.Empty;
        var archiveDate = DateTime.MinValue;

        var urlMatch = OriginalUrlRegex().Match(html);
        if (urlMatch.Success)
        {
            originalUrl = urlMatch.Groups[1].Value.Trim();
        }

        var dateMatch = ArchiveDateRegex().Match(html);
        if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value.Trim(), out var date))
        {
            archiveDate = date;
        }

        return (originalUrl, archiveDate);
    }

    private void RemoveUnwantedElements(HtmlDocument doc)
    {
        foreach (var selector in _options.RemoveSelectors)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{selector}") ??
                        doc.DocumentNode.SelectNodes($"//*[contains(@class, '{selector.TrimStart('.')}')]");

            if (nodes != null)
            {
                foreach (var node in nodes.ToList())
                {
                    node.Remove();
                }
            }
        }
    }

    private HtmlNode? ExtractMainContent(HtmlDocument doc)
    {
        // Try configured selector first
        if (!string.IsNullOrEmpty(_options.ContentSelector))
        {
            var node = doc.DocumentNode.SelectSingleNode($"//{_options.ContentSelector}") ??
                       doc.DocumentNode.SelectSingleNode($"//*[contains(@class, '{_options.ContentSelector.TrimStart('.')}')]");
            if (node != null)
                return node;
        }

        // Try common content selectors
        var commonSelectors = new[]
        {
            "//article",
            "//main",
            "//*[@id='content']",
            "//*[@id='main-content']",
            "//*[contains(@class, 'post-content')]",
            "//*[contains(@class, 'entry-content')]",
            "//*[contains(@class, 'article-content')]",
            "//*[contains(@class, 'blog-post')]",
            "//div[@role='main']"
        };

        foreach (var selector in commonSelectors)
        {
            var node = doc.DocumentNode.SelectSingleNode(selector);
            if (node != null)
                return node;
        }

        // Fall back to body
        return doc.DocumentNode.SelectSingleNode("//body");
    }

    private static string ExtractTitle(HtmlDocument doc, HtmlNode contentNode)
    {
        // Try h1 in content
        var h1 = contentNode.SelectSingleNode(".//h1");
        if (h1 != null)
            return HttpUtility.HtmlDecode(h1.InnerText.Trim());

        // Try title tag
        var title = doc.DocumentNode.SelectSingleNode("//title");
        if (title != null)
        {
            var titleText = HttpUtility.HtmlDecode(title.InnerText.Trim());
            // Remove common suffixes
            var separators = new[] { " | ", " - ", " :: ", " » " };
            foreach (var sep in separators)
            {
                var idx = titleText.IndexOf(sep, StringComparison.Ordinal);
                if (idx > 0)
                {
                    titleText = titleText[..idx].Trim();
                    break;
                }
            }
            return titleText;
        }

        // Try og:title
        var ogTitle = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
        if (ogTitle != null)
            return HttpUtility.HtmlDecode(ogTitle.GetAttributeValue("content", string.Empty));

        return string.Empty;
    }

    private void RewriteLinks(HtmlNode contentNode, string originalUrl)
    {
        if (string.IsNullOrEmpty(originalUrl))
            return;

        Uri? baseUri = null;
        try
        {
            baseUri = new Uri(originalUrl);
        }
        catch
        {
            return;
        }

        var targetHost = !string.IsNullOrEmpty(_archiveOptions.TargetUrl)
            ? new Uri(_archiveOptions.TargetUrl).Host
            : baseUri.Host;

        // Rewrite all href and src attributes
        var linksAndImages = contentNode.SelectNodes(".//*[@href or @src]");
        if (linksAndImages == null)
            return;

        foreach (var node in linksAndImages)
        {
            // Handle href
            var href = node.GetAttributeValue("href", null);
            if (!string.IsNullOrEmpty(href))
            {
                var rewritten = RewriteUrl(href, baseUri, targetHost);
                node.SetAttributeValue("href", rewritten);
            }

            // Handle src
            var src = node.GetAttributeValue("src", null);
            if (!string.IsNullOrEmpty(src))
            {
                var rewritten = RewriteUrl(src, baseUri, targetHost);
                node.SetAttributeValue("src", rewritten);
            }
        }
    }

    private static string RewriteUrl(string url, Uri baseUri, string targetHost)
    {
        // Skip empty, mailto, tel, javascript, and anchor links
        if (string.IsNullOrEmpty(url) ||
            url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith('#'))
        {
            return url;
        }

        // Remove Wayback Machine URL prefix if present
        var waybackMatch = Regex.Match(url, @"https?://web\.archive\.org/web/\d+[a-z_]*/(.+)");
        if (waybackMatch.Success)
        {
            url = waybackMatch.Groups[1].Value;
        }

        // Try to parse as absolute URL
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            // If it's from the same domain, convert to relative
            if (absoluteUri.Host.Equals(targetHost, StringComparison.OrdinalIgnoreCase) ||
                absoluteUri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return absoluteUri.PathAndQuery;
            }

            // External link - keep as-is
            return url;
        }

        // Already relative or invalid - return as-is
        return url;
    }

    private async Task<List<ImageInfo>> ProcessImagesAsync(
        HtmlNode contentNode,
        string originalUrl,
        CancellationToken cancellationToken)
    {
        var images = new List<ImageInfo>();

        if (!_options.PreserveImages)
            return images;

        var imgNodes = contentNode.SelectNodes(".//img[@src]");
        if (imgNodes == null)
            return images;

        Uri? baseUri = null;
        if (!string.IsNullOrEmpty(originalUrl))
        {
            Uri.TryCreate(originalUrl, UriKind.Absolute, out baseUri);
        }

        var imagesDir = Path.Combine(_options.OutputDirectory, _options.ImagesDirectory);

        foreach (var img in imgNodes)
        {
            var src = img.GetAttributeValue("src", null);
            if (string.IsNullOrEmpty(src))
                continue;

            try
            {
                // Resolve to absolute URL
                var absoluteUrl = src;
                if (!Uri.TryCreate(src, UriKind.Absolute, out var imgUri))
                {
                    if (baseUri != null && Uri.TryCreate(baseUri, src, out var resolvedUri))
                    {
                        absoluteUrl = resolvedUri.ToString();
                    }
                    else
                    {
                        continue;
                    }
                }

                // Generate local filename
                var fileName = GenerateImageFileName(absoluteUrl);
                var localPath = Path.Combine(imagesDir, fileName);
                var markdownPath = $"{_options.ImagesDirectory}/{fileName}";

                var imageInfo = new ImageInfo
                {
                    OriginalUrl = absoluteUrl,
                    LocalPath = localPath,
                    MarkdownPath = markdownPath
                };

                // Download if not already exists
                if (!File.Exists(localPath))
                {
                    try
                    {
                        var imageData = await _httpClient.GetByteArrayAsync(absoluteUrl, cancellationToken);
                        await File.WriteAllBytesAsync(localPath, imageData, cancellationToken);
                        imageInfo.Downloaded = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to download image: {Url}", absoluteUrl);
                    }
                }
                else
                {
                    imageInfo.Downloaded = true;
                }

                // Update the img src to the local path
                if (imageInfo.Downloaded)
                {
                    img.SetAttributeValue("src", markdownPath);
                }

                images.Add(imageInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing image: {Src}", src);
            }
        }

        return images;
    }

    private static string GenerateImageFileName(string url)
    {
        var uri = new Uri(url);
        var fileName = Path.GetFileName(uri.AbsolutePath);

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = $"image_{Guid.NewGuid():N}.jpg";
        }

        // Sanitize filename
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            fileName = fileName.Replace(c, '_');
        }

        // Ensure uniqueness by adding hash
        var hash = url.GetHashCode().ToString("X8");
        var ext = Path.GetExtension(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);

        return $"{name}_{hash}{ext}";
    }

    private static DateTime? ExtractPublishDate(HtmlDocument doc, string html)
    {
        // Try common date meta tags
        var metaSelectors = new[]
        {
            "//meta[@property='article:published_time']",
            "//meta[@name='date']",
            "//meta[@name='pubdate']",
            "//meta[@name='DC.date.issued']",
            "//time[@datetime]",
            "//*[@class='date']",
            "//*[@class='post-date']",
            "//*[@class='entry-date']"
        };

        foreach (var selector in metaSelectors)
        {
            var node = doc.DocumentNode.SelectSingleNode(selector);
            if (node != null)
            {
                var dateStr = node.GetAttributeValue("content", null) ??
                              node.GetAttributeValue("datetime", null) ??
                              node.InnerText;

                if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
                {
                    return date;
                }
            }
        }

        // Try to find date patterns in the HTML
        var datePatterns = new[]
        {
            @"(\d{4}-\d{2}-\d{2})",
            @"(\d{1,2}/\d{1,2}/\d{4})",
            @"((?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4})"
        };

        foreach (var pattern in datePatterns)
        {
            var match = Regex.Match(html, pattern);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
            {
                return date;
            }
        }

        return null;
    }

    private static string GenerateSlug(string title, string originalUrl)
    {
        // Try to get slug from URL path first
        if (!string.IsNullOrEmpty(originalUrl))
        {
            try
            {
                var uri = new Uri(originalUrl);
                var path = uri.AbsolutePath.Trim('/');

                // Take the last path segment as slug
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                {
                    var lastSegment = segments[^1];
                    // Remove file extensions
                    lastSegment = Path.GetFileNameWithoutExtension(lastSegment);
                    if (!string.IsNullOrEmpty(lastSegment) && lastSegment != "index")
                    {
                        return SanitizeSlug(lastSegment);
                    }
                }
            }
            catch
            {
                // Fall through to title-based slug
            }
        }

        // Generate from title
        return SanitizeSlug(title);
    }

    private static string SanitizeSlug(string input)
    {
        // Convert to lowercase
        var slug = input.ToLowerInvariant();

        // Replace spaces and common separators with hyphens
        slug = Regex.Replace(slug, @"[\s_]+", "-");

        // Remove non-alphanumeric characters except hyphens
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", string.Empty);

        // Remove multiple consecutive hyphens
        slug = Regex.Replace(slug, @"-+", "-");

        // Trim hyphens from start and end
        slug = slug.Trim('-');

        return slug;
    }

    private static string CleanMarkdown(string markdown)
    {
        // Remove excessive blank lines
        markdown = Regex.Replace(markdown, @"\n{3,}", "\n\n");

        // Remove trailing whitespace from lines
        markdown = Regex.Replace(markdown, @"[ \t]+\n", "\n");

        // Ensure proper spacing around headers
        markdown = Regex.Replace(markdown, @"(\n#{1,6}\s)", "\n$1");

        return markdown.Trim();
    }

    [GeneratedRegex(@"Original URL:\s*(.+?)[\r\n]")]
    private static partial Regex OriginalUrlRegex();

    [GeneratedRegex(@"Archive Date:\s*(.+?)[\r\n]")]
    private static partial Regex ArchiveDateRegex();
}
