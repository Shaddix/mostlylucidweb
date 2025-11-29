using Mostlylucid.Models.Blog;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Shared.Models;
using CategoryWithCount = Mostlylucid.Shared.Models.CategoryWithCount;

namespace Mostlylucid.Blog.ViewServices;

public interface IBlogViewService : IMarkdownFileBlogService
{
   Task<List<string>> GetCategories(bool noTracking = false);

   Task<List<CategoryWithCount>> GetCategoriesWithCount(string language = MarkdownBaseService.EnglishLanguage);
   
   Task<List<BlogPostViewModel>> GetAllPosts();
    Task<List<BlogPostViewModel>> GetPosts(DateTime? startDate = null, string category = "");
    
    Task<List<PostListModel>> GetPostsForRange(DateTime? startDate = null, DateTime? endDate = null, 
        string[]? categories=null, string language = MarkdownBaseService.EnglishLanguage);
    Task<PostListViewModel> GetPostsByCategory(string category, int page = 1, int pageSize = 10, string language = MarkdownBaseService.EnglishLanguage);
    Task<BlogPostViewModel?> GetPost(string slug, string language = "");
    Task<PostListViewModel> GetPagedPosts(int page = 1, int pageSize = 10, string language = MarkdownBaseService.EnglishLanguage, 
        DateTime? startDate = null, DateTime? endDate = null, string? orderBy =null , string? orderDir = null);
    Task<List<PostListModel>> GetPostsForLanguage(DateTime? startDate = null, string category = "", string language = MarkdownBaseService.EnglishLanguage);
    
    
    Task<bool> Delete(string slug, string language);
    Task<string> GetSlug(int id);
    Task<(string Slug, string Language)> GetSlugAndLanguage(int id);

    /// <summary>
    /// Get posts by a list of slugs for a specific language
    /// </summary>
    Task<List<PostListModel>> GetPostsBySlugAsync(List<string> slugs, string language);

    /// <summary>
    /// Get posts for index page with optional filters, including categories.
    /// </summary>
    Task<BlogIndexResult> GetIndexDataAsync(BlogIndexRequest request);

    /// <summary>
    /// Get calendar days with posts for a given month.
    /// </summary>
    Task<List<string>> GetCalendarDaysAsync(int year, int month, string language);

    /// <summary>
    /// Get the date range of all posts for a language.
    /// </summary>
    Task<DateRangeResult> GetDateRangeAsync(string language);

    /// <summary>
    /// Normalize a slug (remove .md, convert underscores/spaces to hyphens, lowercase).
    /// </summary>
    string NormalizeSlug(string slug);
}

public interface IMarkdownFileBlogService
{
    Task<bool> EntryChanged(string slug, string language, string hash);
    Task<bool> EntryExists(string slug, string language);
    Task<BlogPostViewModel> SavePost(string slug, string language,  string markdown);
      
}