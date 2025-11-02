using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.Services.Interfaces;
using Mostlylucid.Services.Markdown.MarkDigExtensions;
using Mostlylucid.Shared;
using Mostlylucid.Shared.Config.Markdown;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.Services.Markdown;

public class MarkdownBlogPopulator(
    MarkdownConfig markdownConfig,
    MarkdownRenderingService markdownRenderingService,
    IServiceProvider serviceProvider,
    ILogger<MarkdownBlogPopulator> logger)
    : IBlogPopulator, IMarkdownBlogService
{


    private ParallelOptions ParallelOptions => new() { MaxDegreeOfParallelism = 4 };

    private static readonly Regex FetchTagRegex = new(
        @"<fetch\s+[^>]*?markdownurl\s*=\s*[""']([^""']+)[""'][^>]*?pollfrequency\s*=\s*[""'](\d+)h?[""'][^>]*?/\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    ///     The method to preload the cache with pages and Languages.
    /// </summary>
    public async Task Populate(CancellationToken token)
    {
        await PopulatePages(token);
    }

    private async Task PopulatePages(CancellationToken token)
    {
        if (PageCacheHelper.GetPageCache() is { Count: > 0 }) return;
        Dictionary<(string slug, string lang), BlogPostDto> pageCache = new();
        var pages = await GetPages();
        foreach (var page in pages)
        {
            if (token.IsCancellationRequested) break;
            pageCache.TryAdd((page.Slug, page.Language), page);
            
           
        }
        if(token.IsCancellationRequested) return;
        PageCacheHelper.SetPageCache(pageCache);
    }
    
    
    public async Task<BlogPostDto> GetPage(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        // Ensure the file exists
        if (!fileInfo.Exists) throw new FileNotFoundException("The specified file does not exist.", filePath);

        // Read all lines from the file
        var markdown = await File.ReadAllTextAsync(filePath);

        // Process fetch tags and expand the markdown BEFORE rendering
        markdown = await ProcessFetchTags(markdown);

        var publishedDate = fileInfo.CreationTime;
        var viewModel = markdownRenderingService.GetPageFromMarkdown(markdown, publishedDate, filePath);

        return viewModel;
    }

    private async Task<string> ProcessFetchTags(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return markdown;

        // Process all fetch tags
        var matches = FetchTagRegex.Matches(markdown);
        if (matches.Count == 0)
            return markdown;

        foreach (Match match in matches)
        {
            var url = match.Groups[1].Value;
            var pollFrequency = int.Parse(match.Groups[2].Value);

            try
            {
                using var scope = serviceProvider.CreateScope();
                var fetchService = scope.ServiceProvider.GetRequiredService<IMarkdownFetchService>();

                // Fetch the markdown (with blogPostId = 0 since we don't have one yet)
                var result = await fetchService.FetchMarkdownAsync(url, pollFrequency, blogPostId: 0);

                if (result.Success && !string.IsNullOrWhiteSpace(result.Content))
                {
                    // Replace the fetch tag with the fetched markdown content
                    markdown = markdown.Replace(match.Value, result.Content);
                    logger.LogInformation("Successfully expanded fetch tag for {Url}", url);
                }
                else
                {
                    logger.LogWarning("Failed to fetch markdown from {Url}: {Error}", url, result.ErrorMessage);
                    // Replace with HTML comment on failure
                    markdown = markdown.Replace(match.Value,
                        $"<!-- Failed to fetch content from {url}: {result.ErrorMessage} -->");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing fetch tag for {Url}", url);
                markdown = markdown.Replace(match.Value,
                    $"<!-- Error fetching content from {url}: {ex.Message} -->");
            }
        }

        return markdown;
    }


    private async Task<List<BlogPostDto>> GetLanguagePages(string language)
    {
        var pages = Directory.GetFiles(markdownConfig.MarkdownPath, "*.md");
        if (language != Constants.EnglishLanguage)
            pages = Directory.GetFiles(markdownConfig.MarkdownTranslatedPath, $"*.{language}.md");

        var pageModels = new List<BlogPostDto>();
        await Parallel.ForEachAsync(pages, ParallelOptions, async (page, ct) =>
        {
            var pageModel = await GetPage(page);
            pageModel.Language = language;
            pageModels.Add(pageModel);
        });
        return pageModels;
    }


    public async Task<List<BlogPostDto>> GetPages()
    {
        var pageList = new ConcurrentBag<BlogPostDto>();
        var languages = LanguageList();
        var pages = await GetLanguagePages(Constants.EnglishLanguage);
        foreach (var page in pages) pageList.Add(page);
        var pageLanguages = languages.Values.SelectMany(x => x).Distinct().ToList();
        await Parallel.ForEachAsync(pageLanguages, ParallelOptions, async (pageLang, ct) =>
        {
            var langPages = await GetLanguagePages(pageLang);
            if (langPages is { Count: > 0 })
                foreach (var page in langPages)
                    pageList.Add(page);
        });
        foreach (var page in pageList)
        {
            var currentPagelangs = languages.Where(x => x.Key == page.Slug).SelectMany(x => x.Value)?.ToList();
            var listLangs = currentPagelangs ?? new List<string>();
            listLangs.Add(Constants.EnglishLanguage);
            page.Languages = listLangs.OrderBy(x => x).ToArray();
        }

        return pageList.ToList();
    }


    public  Dictionary<string, List<string>> LanguageList()
    {
        var pages = Directory.GetFiles(markdownConfig.MarkdownTranslatedPath, "*.md");
        Dictionary<string, List<string>> languageList = new();
        foreach (var page in pages)
        {
            var pageName = Path.GetFileNameWithoutExtension(page);
            var languageCode = pageName.LastIndexOf(".", StringComparison.Ordinal) + 1;
            var language = pageName.Substring(languageCode);
            var originPage = pageName.Substring(0, languageCode - 1);
            if (languageList.TryGetValue(originPage, out var languages))
            {
                languages.Add(language);
                languageList[originPage] = languages;
            }
            else
            {
                languageList[originPage] = new List<string> { language };
            }
        }
        return languageList;
    }


}