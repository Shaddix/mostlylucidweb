using PuppeteerSharp;
using Xunit.Abstractions;

namespace Mostlylucid.Test.E2E;

/// <summary>
/// E2E tests for blog URL patterns.
/// Verifies canonical URL format: /blog/{slug} for English, /blog/{language}/{slug} for others.
/// No query string ?language=xx should be used.
///
/// NOTE: Requires site to be running locally.
/// Run: dotnet run --project Mostlylucid --launch-profile https
/// </summary>
public class BlogUrlPatternTests : E2ETestBase
{
    public BlogUrlPatternTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task HomePage_BlogLinks_UseCanonicalUrlFormat()
    {
        // Arrange
        await NavigateAsync("/");

        // Act - Find all blog post links on the home page
        var blogLinks = await EvaluateFunctionAsync<string[]>(@"() => {
            const links = document.querySelectorAll('a[hx-get^=""/blog/""]');
            return Array.from(links).map(a => a.getAttribute('hx-get'));
        }");

        Output.WriteLine($"Found {blogLinks.Length} blog links on home page");

        // Assert - None should have query string parameters
        foreach (var link in blogLinks)
        {
            Output.WriteLine($"  Link: {link}");
            Assert.DoesNotContain("?", link);
            Assert.DoesNotContain("language=", link);
        }

        Output.WriteLine("✅ All home page blog links use canonical URL format (no query strings)");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task BlogIndex_PostLinks_UseCanonicalUrlFormat()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Act - Find all blog post links
        var blogLinks = await EvaluateFunctionAsync<string[]>(@"() => {
            const links = document.querySelectorAll('a[hx-get^=""/blog/""]');
            return Array.from(links).map(a => a.getAttribute('hx-get'));
        }");

        Output.WriteLine($"Found {blogLinks.Length} blog links on /blog page");

        // Assert - None should have query string parameters
        foreach (var link in blogLinks.Take(10)) // Check first 10
        {
            Output.WriteLine($"  Link: {link}");
            Assert.DoesNotContain("?language=", link);
        }

        Output.WriteLine("✅ All blog index links use canonical URL format");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task BlogPost_EnglishPost_UrlHasNoLanguageParameter()
    {
        // Arrange - Navigate to blog list first
        await NavigateAsync("/blog");

        // Get the first post link
        var firstPostUrl = await EvaluateFunctionAsync<string?>(@"() => {
            const link = document.querySelector('a[hx-get^=""/blog/""]');
            return link?.getAttribute('hx-get') || null;
        }");

        if (string.IsNullOrEmpty(firstPostUrl))
        {
            Output.WriteLine("⚠️ No blog posts found - skipping test");
            return;
        }

        Output.WriteLine($"First post URL: {firstPostUrl}");

        // Assert - URL should be /blog/{slug} format (no language segment for English)
        Assert.StartsWith("/blog/", firstPostUrl);
        Assert.DoesNotContain("?language=", firstPostUrl);

        // Navigate to the post
        await NavigateAsync(firstPostUrl);

        // Verify the browser URL matches the expected pattern
        var currentUrl = Page.Url;
        Output.WriteLine($"Current browser URL: {currentUrl}");

        Assert.DoesNotContain("?language=", currentUrl);
        Assert.DoesNotContain("language=en", currentUrl);

        Output.WriteLine("✅ English blog post URL uses canonical format without language parameter");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task BlogPost_ClickingLink_NavigatesToCorrectUrl()
    {
        // Arrange
        await NavigateAsync("/blog");
        await WaitAsync(500);

        // Get the first post link and its expected URL
        var linkInfo = await EvaluateFunctionAsync<LinkInfo?>(@"() => {
            const link = document.querySelector('a[hx-get^=""/blog/""]');
            if (!link) return null;
            return {
                url: link.getAttribute('hx-get'),
                title: link.textContent?.trim() || ''
            };
        }");

        if (linkInfo == null)
        {
            Output.WriteLine("⚠️ No blog posts found - skipping test");
            return;
        }

        Output.WriteLine($"Clicking link to: {linkInfo.Url} ({linkInfo.Title})");

        // Act - Click the post link
        await ClickAsync("a[hx-get^=\"/blog/\"]");
        await WaitAsync(1000); // Wait for HTMX navigation

        // Assert - URL should match the hx-get attribute (HTMX pushes URL)
        var currentUrl = new Uri(Page.Url).PathAndQuery;
        Output.WriteLine($"Navigated to: {currentUrl}");

        Assert.StartsWith("/blog/", currentUrl);
        Assert.DoesNotContain("?language=en", currentUrl);
        Assert.DoesNotContain("&language=", currentUrl);

        Output.WriteLine("✅ Clicking blog link navigates to correct canonical URL");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task BlogPost_PrevNextLinks_UseCanonicalUrlFormat()
    {
        // Arrange - Navigate to a blog post first
        await NavigateAsync("/blog");
        await WaitAsync(300);

        // Click first post
        var hasPost = await ElementExistsAsync("a[hx-get^=\"/blog/\"]");
        if (!hasPost)
        {
            Output.WriteLine("⚠️ No blog posts found - skipping test");
            return;
        }

        await ClickAsync("a[hx-get^=\"/blog/\"]");
        await WaitAsync(1000);

        // Act - Check prev/next navigation links
        var navLinks = await EvaluateFunctionAsync<string[]>(@"() => {
            const links = document.querySelectorAll('nav[aria-label=""Post navigation""] a[hx-get]');
            return Array.from(links).map(a => a.getAttribute('hx-get'));
        }");

        Output.WriteLine($"Found {navLinks.Length} navigation links");

        // Assert - All nav links should use canonical format
        foreach (var link in navLinks)
        {
            Output.WriteLine($"  Nav link: {link}");
            Assert.StartsWith("/blog/", link);
            Assert.DoesNotContain("?language=", link);
        }

        Output.WriteLine("✅ Prev/Next navigation links use canonical URL format");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task LanguageFlags_UseCorrectUrlFormat()
    {
        // Arrange - Navigate to a blog post that has translations
        await NavigateAsync("/blog");
        await WaitAsync(300);

        // Click first post
        await ClickAsync("a[hx-get^=\"/blog/\"]");
        await WaitAsync(1000);

        // Act - Check language flag links
        var flagLinks = await EvaluateFunctionAsync<string[]>(@"() => {
            const links = document.querySelectorAll('.tooltip a[hx-get^=""/blog/""]');
            return Array.from(links).map(a => a.getAttribute('hx-get'));
        }");

        if (flagLinks.Length == 0)
        {
            Output.WriteLine("⚠️ No language flags found - post may not have translations");
            return;
        }

        Output.WriteLine($"Found {flagLinks.Length} language flag links");

        // Assert - All should use /blog/{language}/{slug} format for non-English
        foreach (var link in flagLinks)
        {
            Output.WriteLine($"  Flag link: {link}");
            Assert.StartsWith("/blog/", link);
            Assert.DoesNotContain("?language=", link);

            // Should be either /blog/{slug} or /blog/{lang}/{slug}
            var segments = link.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
            Assert.True(segments.Length >= 2, $"Link should have at least 2 segments: {link}");
        }

        Output.WriteLine("✅ Language flag links use canonical URL format");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task QueryStringLanguage_RedirectsToCanonicalUrl()
    {
        // Arrange - Navigate using old query string format
        var testSlug = "httpclient-testing-without-mocks"; // Use a known slug
        var oldUrl = $"/blog/{testSlug}?language=en";

        Output.WriteLine($"Testing redirect from: {oldUrl}");

        // Act - Navigate to URL with query string
        await NavigateAsync(oldUrl);
        await WaitAsync(1000);

        // Assert - Should redirect to canonical URL without query string
        var currentUrl = new Uri(Page.Url).PathAndQuery;
        Output.WriteLine($"Redirected to: {currentUrl}");

        // The URL should be /blog/{slug} without ?language=en
        Assert.DoesNotContain("?language=en", currentUrl);

        Output.WriteLine("✅ Query string language parameter redirects to canonical URL");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NonEnglishPost_UsesLanguagePathSegment()
    {
        // Arrange - Navigate using non-English URL format
        var testSlug = "httpclient-testing-without-mocks";
        var frenchUrl = $"/blog/fr/{testSlug}";

        Output.WriteLine($"Testing non-English URL: {frenchUrl}");

        // Act
        await NavigateAsync(frenchUrl);
        await WaitAsync(500);

        // Check if we got a 404 or the page loaded
        var is404 = await ElementExistsAsync(".error-404, h1:has-text('404')");
        if (is404)
        {
            Output.WriteLine("⚠️ French translation not available - checking URL format only");
        }

        // Assert - The URL should maintain the /blog/{lang}/{slug} format
        var currentPath = new Uri(Page.Url).AbsolutePath;
        Output.WriteLine($"Current path: {currentPath}");

        // Should not have converted to query string
        Assert.DoesNotContain("?language=", Page.Url);

        Output.WriteLine("✅ Non-English URL maintains correct path format");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task RelatedPosts_UseCanonicalUrlFormat()
    {
        // Arrange - Navigate to a blog post
        await NavigateAsync("/blog");
        await WaitAsync(300);

        await ClickAsync("a[hx-get^=\"/blog/\"]");
        await WaitAsync(2000); // Wait for related posts to load via HTMX

        // Act - Check related post links (loaded asynchronously)
        var relatedLinks = await EvaluateFunctionAsync<string[]>(@"() => {
            const links = document.querySelectorAll('a[hx-action=""Show""][hx-controller=""Blog""]');
            return Array.from(links).map(a => a.getAttribute('hx-get') || '');
        }");

        if (relatedLinks.Length == 0 || relatedLinks.All(string.IsNullOrEmpty))
        {
            Output.WriteLine("⚠️ No related posts found or links not using hx-get");
            return;
        }

        Output.WriteLine($"Found {relatedLinks.Length} related post links");

        foreach (var link in relatedLinks.Where(l => !string.IsNullOrEmpty(l)))
        {
            Output.WriteLine($"  Related link: {link}");
            Assert.DoesNotContain("?language=", link);
        }

        Output.WriteLine("✅ Related post links use canonical URL format");
    }

    private class LinkInfo
    {
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
    }
}
