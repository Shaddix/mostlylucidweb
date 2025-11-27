using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Mostlylucid.Controllers;
using Mostlylucid.Services;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Shared.Config.Markdown;

namespace Mostlylucid.API;

[ApiController]
[Route("api/raw")]
public class RawController(
    MarkdownConfig markdownConfig,
    BaseControllerService baseControllerService,
    ILogger<RawController> logger) : BaseController(baseControllerService, logger)
{
    private static readonly Regex SafeSlug = new("^[a-z0-9-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Returns the raw on-disk Markdown for a blog post.
    /// English lives in MarkdownPath: {slug}.md
    /// Translations live in MarkdownTranslatedPath: {slug}.{language}.md
    /// </summary>
    /// <param name="slug">The post slug (case-insensitive)</param>
    /// <param name="language">Language code, defaults to en</param>
    [HttpGet("{slug}")]
    [ResponseCache(Duration = 300, VaryByHeader = "hx-request", VaryByQueryKeys = new[] { nameof(language) }, Location = ResponseCacheLocation.Client)]
    [OutputCache(Duration = 3600, VaryByQueryKeys = new[] { nameof(language) })]
    public IActionResult GetRaw(string slug, string language = MarkdownBaseService.EnglishLanguage)
    {
        if (string.IsNullOrWhiteSpace(slug)) return BadRequest("Slug is required");

        // Normalize the slug similarly to BlogController
        slug = slug.Trim();
        if (slug.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) slug = slug[..^3];
        slug = slug.Replace('_', '-').Replace(' ', '-').ToLowerInvariant();

        // Whitelist allowed characters to avoid path traversal
        if (!SafeSlug.IsMatch(slug)) return BadRequest("Invalid slug format");

        var lang = string.IsNullOrWhiteSpace(language) ? MarkdownBaseService.EnglishLanguage : language.ToLowerInvariant();

        string path = lang == MarkdownBaseService.EnglishLanguage
            ? Path.Combine(markdownConfig.MarkdownPath, slug + ".md")
            : Path.Combine(markdownConfig.MarkdownTranslatedPath, $"{slug}.{lang}.md");

        if (!System.IO.File.Exists(path))
        {
            // Fallback to English if translation not found
            if (lang != MarkdownBaseService.EnglishLanguage)
            {
                var fallback = Path.Combine(markdownConfig.MarkdownPath, slug + ".md");
                if (System.IO.File.Exists(fallback))
                {
                    path = fallback;
                    lang = MarkdownBaseService.EnglishLanguage;
                }
            }
        }

        if (!System.IO.File.Exists(path)) return NotFound();

        // Stream the file as text/markdown; charset=utf-8
        var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var contentType = "text/markdown; charset=utf-8";
        var downloadName = Path.GetFileName(path);

        // Provide a strong ETag/Last-Modified via physical file metadata
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{downloadName}\"";
        return File(fileStream, contentType);
    }
}
