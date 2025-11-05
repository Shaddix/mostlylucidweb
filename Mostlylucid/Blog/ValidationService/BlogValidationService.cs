using System.Text.RegularExpressions;
using Mostlylucid.Services.Blog;
using Mostlylucid.Services.Interfaces;
using Mostlylucid.Shared.Config.Markdown;

namespace Mostlylucid.Blog.ValidationService;

/// <summary>
/// Service to validate blog content integrity on deployment
/// Checks for broken links, missing files, orphaned entries, etc.
/// </summary>
public partial class BlogValidationService(
    MarkdownConfig markdownConfig,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<BlogValidationService> logger)
{
    [GeneratedRegex(@"\[([^\]]+)\]\(([^\)]+)\)", RegexOptions.Compiled)]
    private static partial Regex MarkdownLinkRegex();

    public async Task<ValidationResult> ValidateAsync()
    {
        var result = new ValidationResult();
        logger.LogInformation("Starting blog validation");

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var blogService = scope.ServiceProvider.GetRequiredService<IBlogService>();

            // 1. Get all markdown files
            var markdownFiles = Directory.GetFiles(markdownConfig.MarkdownPath, "*.md", SearchOption.TopDirectoryOnly);
            var translatedFiles = Directory.GetFiles(markdownConfig.MarkdownTranslatedPath, "*.md", SearchOption.TopDirectoryOnly);

            result.TotalMarkdownFiles = markdownFiles.Length;
            result.TotalTranslatedFiles = translatedFiles.Length;

            // 2. Get all database posts
            var allPosts = await blogService.GetAllPosts();
            result.TotalDatabasePosts = allPosts.Count;

            // 3. Build expected entries map
            var expectedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in markdownFiles)
            {
                var slug = Path.GetFileNameWithoutExtension(file);
                expectedEntries.Add($"{slug}|en");
            }

            foreach (var file in translatedFiles)
            {
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                if (fileNameWithoutExt.Contains("."))
                {
                    var parts = fileNameWithoutExt.Split('.');
                    var slug = parts[0];
                    var language = parts[^1];
                    expectedEntries.Add($"{slug}|{language}");
                }
            }

            // 4. Find orphaned database entries
            var orphanedPosts = allPosts
                .Where(post =>
                {
                    var key = $"{post.Slug}|{post.Language}";
                    return !expectedEntries.Contains(key);
                })
                .ToList();

            result.OrphanedDatabaseEntries = orphanedPosts.Count;
            result.OrphanedPosts = orphanedPosts.Select(p => $"{p.Slug} ({p.Language})").ToList();

            // 5. Find missing database entries
            var existingKeys = allPosts.Select(p => $"{p.Slug}|{p.Language}").ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingInDb = expectedEntries.Except(existingKeys, StringComparer.OrdinalIgnoreCase).ToList();

            result.MissingDatabaseEntries = missingInDb.Count;
            result.MissingInDatabase = missingInDb;

            // 6. Validate internal markdown links
            await ValidateMarkdownLinksAsync(markdownFiles, expectedEntries, result);

            // 7. Check for slug collisions (case-insensitive duplicates)
            var slugs = allPosts.Select(p => p.Slug.ToLowerInvariant()).ToList();
            var duplicates = slugs.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            if (duplicates.Any())
            {
                result.SlugCollisions = duplicates.Count;
                result.CollisionSlugs = duplicates;
            }

            result.IsValid = result.OrphanedDatabaseEntries == 0 &&
                            result.MissingDatabaseEntries == 0 &&
                            result.BrokenLinks.Count == 0 &&
                            result.SlugCollisions == 0;

            var status = result.IsValid ? "PASSED" : "FAILED";
            logger.LogInformation(
                "Blog validation {Status}: {Total} files, {DbPosts} DB posts, {Orphaned} orphaned, {Missing} missing, {Broken} broken links, {Collisions} slug collisions",
                status,
                result.TotalMarkdownFiles,
                result.TotalDatabasePosts,
                result.OrphanedDatabaseEntries,
                result.MissingDatabaseEntries,
                result.BrokenLinks.Count,
                result.SlugCollisions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during blog validation");
            result.IsValid = false;
            result.ValidationError = ex.Message;
        }

        return result;
    }

    private async Task ValidateMarkdownLinksAsync(
        string[] markdownFiles,
        HashSet<string> expectedEntries,
        ValidationResult result)
    {
        foreach (var file in markdownFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var links = MarkdownLinkRegex().Matches(content);

                foreach (Match match in links)
                {
                    var linkUrl = match.Groups[2].Value;

                    // Only check internal .md file links
                    if (!linkUrl.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (linkUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        linkUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Extract slug from link
                    var linkedSlug = Path.GetFileNameWithoutExtension(linkUrl);
                    linkedSlug = linkedSlug.Replace('_', '-')
                                          .Replace(' ', '-')
                                          .ToLowerInvariant();

                    // Check if target exists (English version)
                    if (!expectedEntries.Contains($"{linkedSlug}|en"))
                    {
                        var sourceFile = Path.GetFileName(file);
                        result.BrokenLinks.Add($"{sourceFile} -> {linkUrl} (target not found)");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error validating links in {File}", file);
            }
        }
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public int TotalMarkdownFiles { get; set; }
    public int TotalTranslatedFiles { get; set; }
    public int TotalDatabasePosts { get; set; }
    public int OrphanedDatabaseEntries { get; set; }
    public int MissingDatabaseEntries { get; set; }
    public int SlugCollisions { get; set; }
    public List<string> OrphanedPosts { get; set; } = new();
    public List<string> MissingInDatabase { get; set; } = new();
    public List<string> BrokenLinks { get; set; } = new();
    public List<string> CollisionSlugs { get; set; } = new();
    public string? ValidationError { get; set; }

    public override string ToString()
    {
        var lines = new List<string>
        {
            $"=== Blog Validation Report ===",
            $"Status: {(IsValid ? "✓ PASSED" : "✗ FAILED")}",
            $"",
            $"Files:",
            $"  - Markdown files: {TotalMarkdownFiles}",
            $"  - Translated files: {TotalTranslatedFiles}",
            $"  - Database posts: {TotalDatabasePosts}",
            $"",
            $"Issues:",
            $"  - Orphaned DB entries: {OrphanedDatabaseEntries}",
            $"  - Missing DB entries: {MissingDatabaseEntries}",
            $"  - Broken links: {BrokenLinks.Count}",
            $"  - Slug collisions: {SlugCollisions}"
        };

        if (OrphanedPosts.Any())
        {
            lines.Add("");
            lines.Add("Orphaned Posts:");
            lines.AddRange(OrphanedPosts.Select(p => $"  - {p}"));
        }

        if (MissingInDatabase.Any())
        {
            lines.Add("");
            lines.Add("Missing in Database:");
            lines.AddRange(MissingInDatabase.Take(10).Select(m => $"  - {m}"));
            if (MissingInDatabase.Count > 10)
                lines.Add($"  ... and {MissingInDatabase.Count - 10} more");
        }

        if (BrokenLinks.Any())
        {
            lines.Add("");
            lines.Add("Broken Links:");
            lines.AddRange(BrokenLinks.Take(10).Select(l => $"  - {l}"));
            if (BrokenLinks.Count > 10)
                lines.Add($"  ... and {BrokenLinks.Count - 10} more");
        }

        if (CollisionSlugs.Any())
        {
            lines.Add("");
            lines.Add("Slug Collisions:");
            lines.AddRange(CollisionSlugs.Select(s => $"  - {s}"));
        }

        if (!string.IsNullOrEmpty(ValidationError))
        {
            lines.Add("");
            lines.Add($"Error: {ValidationError}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
