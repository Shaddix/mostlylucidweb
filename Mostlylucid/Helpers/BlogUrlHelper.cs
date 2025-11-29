using Mostlylucid.Shared;

namespace Mostlylucid.Helpers;

/// <summary>
/// Helper class for generating consistent blog URLs.
/// Pattern: /blog/{slug} for English, /blog/{language}/{slug} for other languages.
/// </summary>
public static class BlogUrlHelper
{
    /// <summary>
    /// Generates a relative blog post URL.
    /// </summary>
    /// <param name="slug">The post slug</param>
    /// <param name="language">The language code (defaults to English)</param>
    /// <returns>Relative URL path</returns>
    public static string GetBlogUrl(string slug, string? language = null)
    {
        var isEnglish = string.IsNullOrEmpty(language) ||
                        language.Equals(Constants.EnglishLanguage, StringComparison.OrdinalIgnoreCase);

        return isEnglish ? $"/blog/{slug}" : $"/blog/{language}/{slug}";
    }

    /// <summary>
    /// Checks if a language code represents English.
    /// </summary>
    /// <param name="language">The language code</param>
    /// <returns>True if English or empty/null</returns>
    public static bool IsEnglish(string? language)
    {
        return string.IsNullOrEmpty(language) ||
               language.Equals(Constants.EnglishLanguage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generates an absolute blog post URL.
    /// </summary>
    /// <param name="slug">The post slug</param>
    /// <param name="language">The language code (defaults to English)</param>
    /// <param name="host">The host (e.g., "mostlylucid.net")</param>
    /// <param name="scheme">The scheme (defaults to "https")</param>
    /// <returns>Absolute URL</returns>
    public static string GetAbsoluteBlogUrl(string slug, string? language, string host, string scheme = "https")
    {
        var relativePath = GetBlogUrl(slug, language);
        return $"{scheme}://{host}{relativePath}";
    }
}
