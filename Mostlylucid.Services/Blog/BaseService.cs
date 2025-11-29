using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Shared;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Shared.Helpers;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.Services.Blog;

public class BaseService(IMostlylucidDBContext context, ILogger<BaseService> logger)
{
    protected readonly IMostlylucidDBContext Context = context;
    protected readonly ILogger<BaseService> Logger = logger;

    public async Task<List<string>> GetCategories(bool noTracking = false)
    {
        // Fetch blog posts in English and get their associated categories
        var catQuery = Context.BlogPosts
            .Where(x => x.LanguageEntity.Name == Constants.EnglishLanguage)
            .SelectMany(x => x.Categories)
            .GroupBy(x => x.Name)
            .Select(g => g.Key); // Select distinct category names

        // Apply no-tracking if specified
        if (noTracking)
        {
            return await catQuery
                .AsNoTracking()
                .ToListAsync();
        }

        return await catQuery.ToListAsync();
    }


    protected IQueryable<BlogPostEntity> PostsQuery()
    {
        return Context.BlogPosts.Include(x => x.Categories)
            .Include(x => x.LanguageEntity);
    }

    protected async Task<BlogPostEntity?> SavePost(BlogPostDto post, BlogPostEntity? currentPost = null,
        List<CategoryEntity>? categories = null,
        List<LanguageEntity>? languages = null, Activity? activity = null)
    {
        if (languages == null)
            languages = await Context.Languages.ToListAsync();

        var postLanguageEntity = languages.FirstOrDefault(x => x.Name == post.Language);
        if (postLanguageEntity == null)
        {
            // Auto-create the language if it doesn't exist
            Logger.LogInformation("Creating new language entity for {Language}", post.Language);
            postLanguageEntity = new LanguageEntity { Name = post.Language };
            Context.Languages.Add(postLanguageEntity);
            await Context.SaveChangesAsync(); // Save immediately so it gets an ID
        }

        categories ??= await Context.Categories.Where(x => post.Categories.Contains(x.Name)).ToListAsync();
        currentPost ??= await PostsQuery().Where(x => x.Slug == post.Slug && x.LanguageEntity == postLanguageEntity)
            .FirstOrDefaultAsync();
        try
        {
            var hash = post.Markdown.ContentHash();
            if(string.IsNullOrEmpty(hash))
            {
                activity?.AddTag("Empty Hash", post.Slug);
                Logger.LogError("Empty hash for post {Post}", post.Slug);
                return null;
            }
            var hashChanged = currentPost?.ContentHash != hash;

            // Debug logging
            Logger.LogInformation("Hash check for {Slug}: New={NewHash}, Old={OldHash}, Changed={Changed}, MarkdownLength={Length}",
                post.Slug, hash, currentPost?.ContentHash ?? "NULL", hashChanged, post.Markdown?.Length ?? 0);

            //Add an inital check, if the current post is the same as the new post's hash, then we can skip the rest of the checks
            if (!hashChanged)
            {
                activity?.AddTag("Post Hash Not Changed", post.Slug);
                Logger.LogInformation("Post Hash {Post} for language {Language} has not changed", post.Slug,
                    post.Language);
                return currentPost;
            }

            // If this is a new post, check if another post in the same language already has this ContentHash
            // This prevents duplicate inserts when file watcher triggers multiple rapid events
            // Check both the database AND the local context tracker (for batch operations like BlogPopulator)
            if (currentPost == null)
            {
                // Check database
                var existingInDb = await Context.BlogPosts
                    .AnyAsync(x => x.ContentHash == hash && x.LanguageEntity.Name == post.Language);

                // Check local tracker (entities added but not yet saved)
                // Note: Local may not be available in test scenarios with mocked DbContext
                var existingInLocal = false;
                try
                {
                    var local = Context.BlogPosts.Local;
                    if (local != null)
                    {
                        existingInLocal = local
                            .Any(x => x.ContentHash == hash && x.LanguageEntity?.Name == post.Language);
                    }
                }
                catch
                {
                    // Local is not available (e.g., in unit tests with mocked DbContext)
                }

                if (existingInDb || existingInLocal)
                {
                    Logger.LogWarning("Post with ContentHash {Hash} already exists for language {Language}, skipping duplicate insert for {Slug}",
                        hash, post.Language, post.Slug);
                    return null;
                }
            }

            foreach (var postCat in post.Categories)
            {
                if (categories.All(x => x.Name != postCat))
                {
                    categories.Add(new CategoryEntity() { Name = postCat });
                }
            }

            var blogPost = currentPost ?? new BlogPostEntity();
            blogPost.Title = post.Title;
            blogPost.Slug = post.Slug;
            blogPost.Markdown = post.Markdown;
            blogPost.HtmlContent = post.HtmlContent;
            blogPost.PlainTextContent = post.PlainTextContent;
            blogPost.ContentHash = hash;
            blogPost.PublishedDate = post.PublishedDate;
            blogPost.LanguageEntity = postLanguageEntity;
            blogPost.Categories = categories.Where(x => post.Categories.Contains(x.Name)).ToList();
            if (currentPost != null)
                blogPost.UpdatedDate = DateTimeOffset.UtcNow;
            if (currentPost != null)
            {
                activity?.AddTag("Updating Post", post.Slug);
                Logger.LogInformation("Updating post {Post}", post.Slug);
                Context.BlogPosts.Update(blogPost); // Update the existing post
            }
            else
            {
                logger.LogInformation("Adding new post {Post}", post.Slug);
                Context.BlogPosts.Add(blogPost); // Add a new post
            }

            return blogPost;
        }
        catch (Exception e)
        {
            activity?.AddTag("Error Adding Post", post.Slug);
            logger.LogError(e, "Error adding post {Post}", post.Slug);
        }

        return null;
    }
}