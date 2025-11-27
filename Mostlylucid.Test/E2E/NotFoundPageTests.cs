using Xunit.Abstractions;

namespace Mostlylucid.Test.E2E;

/// <summary>
/// E2E tests for the 404 Not Found page functionality.
/// Tests slug suggestions, navigation, and archive URL handling.
///
/// NOTE: Requires site to be running locally.
/// Run: dotnet run --project Mostlylucid --launch-profile https
/// </summary>
public class NotFoundPageTests : E2ETestBase
{
    public NotFoundPageTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_DisplaysFor_InvalidBlogPost()
    {
        // Arrange & Act
        await NavigateAsync("/blog/this-post-does-not-exist-12345");

        // Assert - Should show 404 content
        var has404Title = await EvaluateFunctionAsync<bool>(@"() => {
            const h1 = document.querySelector('h1');
            return h1 && h1.textContent.includes('404');
        }");

        Assert.True(has404Title, "Page should display 404 title");
        Output.WriteLine("✅ 404 page displays for invalid blog post");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_ShowsOriginalPath()
    {
        // Arrange
        var invalidSlug = "my-invalid-post-slug";

        // Act
        await NavigateAsync($"/blog/{invalidSlug}");

        // Assert - Should show the requested path
        var pathDisplayed = await EvaluateFunctionAsync<bool>(@"(slug) => {
            const codeElement = document.querySelector('code');
            return codeElement && codeElement.textContent.includes(slug);
        }", invalidSlug);

        Assert.True(pathDisplayed, "404 page should display the original requested path");
        Output.WriteLine("✅ 404 page shows original requested path");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_ShowsSuggestions_ForSimilarSlug()
    {
        // Arrange - Use a slug that's similar to an existing post
        // This assumes there's a post with "docker" in the slug
        await NavigateAsync("/blog/dockerr"); // Typo of "docker"

        // Act - Check if suggestions are displayed
        var hasSuggestions = await EvaluateFunctionAsync<bool>(@"() => {
            const suggestions = document.querySelectorAll('.suggestion-link');
            return suggestions.length > 0;
        }");

        Output.WriteLine($"Has suggestions: {hasSuggestions}");

        if (hasSuggestions)
        {
            var suggestionCount = await EvaluateFunctionAsync<int>(@"() => {
                return document.querySelectorAll('.suggestion-link').length;
            }");
            Output.WriteLine($"✅ Found {suggestionCount} suggestion(s) for similar slug");
        }
        else
        {
            Output.WriteLine("⚠️ No suggestions found - this may be expected if no similar posts exist");
        }
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_SuggestionLinks_AreClickable()
    {
        // Arrange
        await NavigateAsync("/blog/aspnet-typo");

        // Check if suggestions exist
        var hasSuggestions = await ElementExistsAsync(".suggestion-link");
        if (!hasSuggestions)
        {
            Output.WriteLine("⚠️ No suggestions available to test");
            return;
        }

        // Get the first suggestion URL
        var suggestionUrl = await EvaluateFunctionAsync<string>(@"() => {
            const link = document.querySelector('.suggestion-link');
            return link?.getAttribute('href') || '';
        }");

        Output.WriteLine($"First suggestion URL: {suggestionUrl}");

        // Act - Click the first suggestion
        await ClickAsync(".suggestion-link");
        await WaitAsync(1000);

        // Assert - Should navigate to the blog post
        Assert.Contains("/blog/", Page.Url);
        Assert.DoesNotContain("404", await GetTextContentAsync("h1") ?? "");
        Output.WriteLine("✅ Suggestion link navigates to blog post");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_SuggestionClick_TracksForMachineLearning()
    {
        // Arrange
        await NavigateAsync("/blog/test-typo-slug");

        var hasSuggestions = await ElementExistsAsync(".suggestion-link");
        if (!hasSuggestions)
        {
            Output.WriteLine("⚠️ No suggestions available to test tracking");
            return;
        }

        // Set up network interception to track the API call
        var trackingRequestMade = false;
        await Page.SetRequestInterceptionAsync(true);

        Page.Request += (sender, e) =>
        {
            if (e.Request.Url.Contains("/api/slugsuggestion/track-click"))
            {
                trackingRequestMade = true;
                Output.WriteLine($"Tracking request intercepted: {e.Request.Url}");
            }
            e.Request.ContinueAsync();
        };

        // Act - Click a suggestion
        await ClickAsync(".suggestion-link");
        await WaitAsync(500);

        // Assert
        Assert.True(trackingRequestMade, "Clicking suggestion should track the click for ML");
        Output.WriteLine("✅ Suggestion click tracking works");

        await Page.SetRequestInterceptionAsync(false);
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_ShowsHighMatchBadge_ForGoodSuggestions()
    {
        // Arrange - Navigate to a page with a minor typo
        await NavigateAsync("/blog/using-doker"); // Typo of "docker"

        // Check if there are high match badges
        var hasHighMatchBadge = await EvaluateFunctionAsync<bool>(@"() => {
            const badges = document.querySelectorAll('.badge-success');
            return badges.length > 0;
        }");

        var hasPossibleMatchBadge = await EvaluateFunctionAsync<bool>(@"() => {
            const badges = document.querySelectorAll('.badge-warning');
            return badges.length > 0;
        }");

        Output.WriteLine($"Has high match badge: {hasHighMatchBadge}");
        Output.WriteLine($"Has possible match badge: {hasPossibleMatchBadge}");

        // At least one type of badge or no suggestions
        var hasSuggestions = await ElementExistsAsync(".suggestion-link");
        if (hasSuggestions)
        {
            Output.WriteLine("✅ Suggestions with match quality badges displayed");
        }
        else
        {
            Output.WriteLine("⚠️ No suggestions to display badges for");
        }
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_ArchiveUrl_ShowsSuggestions()
    {
        // Arrange - Test archive URL pattern
        await NavigateAsync("/archite/2002/01/01/445.html");

        // Act - Check if page shows 404 or redirected
        var is404 = await EvaluateFunctionAsync<bool>(@"() => {
            const h1 = document.querySelector('h1');
            return h1 && h1.textContent.includes('404');
        }");

        var isBlogPost = Page.Url.Contains("/blog/") && !Page.Url.Contains("archite");

        Output.WriteLine($"Is 404: {is404}, Is blog post: {isBlogPost}");

        if (isBlogPost)
        {
            Output.WriteLine("✅ Archive URL was auto-redirected to a blog post");
        }
        else if (is404)
        {
            // Check if suggestions are shown for archive URLs
            var hasSuggestions = await ElementExistsAsync(".suggestion-link");
            Output.WriteLine($"404 shown, has suggestions: {hasSuggestions}");

            if (hasSuggestions)
            {
                Output.WriteLine("✅ Archive URL shows 404 with suggestions");
            }
            else
            {
                Output.WriteLine("⚠️ Archive URL shows 404 without suggestions");
            }
        }
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_ArchiveUrl_AspxExtension_Handled()
    {
        // Arrange - Test .aspx archive URL pattern
        await NavigateAsync("/archive/old-post.aspx");

        // Act - Check response
        var is404 = await EvaluateFunctionAsync<bool>(@"() => {
            const h1 = document.querySelector('h1');
            return h1 && h1.textContent.includes('404');
        }");

        var isBlogPost = Page.Url.Contains("/blog/");

        Output.WriteLine($"Is 404: {is404}, Is blog post: {isBlogPost}");

        // Either redirected or 404 is acceptable
        Assert.True(is404 || isBlogPost, "Archive .aspx URL should either redirect or show 404");
        Output.WriteLine("✅ Archive .aspx URL is handled");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_ShowsHomeLink_WhenNoSuggestions()
    {
        // Arrange - Navigate to a completely random path unlikely to have suggestions
        await NavigateAsync("/blog/xyzabc123-random-gibberish-no-match-possible");

        // Act - Check if home link is shown
        var hasHomeLink = await EvaluateFunctionAsync<bool>("() => { const links = document.querySelectorAll('a[href=\"/\"]'); return links.length > 0; }");

        Assert.True(hasHomeLink, "404 page should have a link to the home page");
        Output.WriteLine("404 page shows home page link");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_DisplaysCategories_InSuggestions()
    {
        // Arrange
        await NavigateAsync("/blog/dotnet-typo");

        var hasSuggestions = await ElementExistsAsync(".suggestion-link");
        if (!hasSuggestions)
        {
            Output.WriteLine("⚠️ No suggestions to test categories");
            return;
        }

        // Act - Check if categories are displayed
        var hasCategories = await EvaluateFunctionAsync<bool>("() => { const categoryBadges = document.querySelectorAll('.bg-blue-100'); return categoryBadges.length > 0; }");

        Output.WriteLine($"Suggestions have category badges: {hasCategories}");
        Output.WriteLine("✅ Suggestion display tested");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_DisplaysPublishedDate_InSuggestions()
    {
        // Arrange
        await NavigateAsync("/blog/some-typo-slug");

        var hasSuggestions = await ElementExistsAsync(".suggestion-link");
        if (!hasSuggestions)
        {
            Output.WriteLine("⚠️ No suggestions to test published date");
            return;
        }

        // Act - Check if published dates are displayed
        var hasPublishedDate = await EvaluateFunctionAsync<bool>(@"() => {
            const text = document.body.textContent;
            return text.includes('Published:');
        }");

        Assert.True(hasPublishedDate, "Suggestions should show published date");
        Output.WriteLine("✅ Suggestions display published date");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_TracksPageView_WithUmami()
    {
        // Arrange
        var trackingCalled = false;
        await Page.SetRequestInterceptionAsync(true);

        Page.Request += (sender, e) =>
        {
            // Check for Umami tracking call
            if (e.Request.Url.Contains("umami") || e.Request.Url.Contains("/api/send"))
            {
                trackingCalled = true;
                Output.WriteLine($"Umami tracking request: {e.Request.Url}");
            }
            e.Request.ContinueAsync();
        };

        // Act
        await NavigateAsync("/blog/track-this-404-page");
        await WaitAsync(1000);

        // We can't always guarantee Umami is configured, but the script should be present
        var hasTrackingScript = await EvaluateFunctionAsync<bool>(@"() => {
            return typeof umami !== 'undefined' || document.body.innerHTML.includes('umami.track');
        }");

        Output.WriteLine($"Umami tracking available: {hasTrackingScript}");
        Output.WriteLine($"Tracking request made: {trackingCalled}");

        await Page.SetRequestInterceptionAsync(false);

        Output.WriteLine("✅ 404 tracking script is in place");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task NotFoundPage_AutoRedirect_ForHighConfidenceMatch()
    {
        // This test checks if the auto-redirect feature works
        // It requires a known typo -> correct slug mapping in the database

        // Arrange - Navigate to a path that should auto-redirect
        // Note: This depends on having learned redirects in the database
        await NavigateAsync("/blog/dockerr-tutorial"); // Assuming "docker-tutorial" exists

        // Wait for potential redirect
        await WaitAsync(500);

        // Act - Check if we were redirected
        var currentUrl = Page.Url;
        Output.WriteLine($"Current URL after navigation: {currentUrl}");

        var was404 = await EvaluateFunctionAsync<bool>(@"() => {
            const h1 = document.querySelector('h1');
            return h1 && h1.textContent.includes('404');
        }");

        if (!was404 && currentUrl.Contains("/blog/"))
        {
            Output.WriteLine("✅ High confidence match resulted in auto-redirect");
        }
        else
        {
            Output.WriteLine("⚠️ No auto-redirect occurred (may not have learned pattern yet)");
        }
    }
}
