using Xunit.Abstractions;

namespace Mostlylucid.Test.E2E;

/// <summary>
/// E2E tests for the announcement banner functionality.
/// Tests banner display, dismiss animation, and persistence.
///
/// NOTE: Requires site to be running locally with an active announcement.
/// Run: dotnet run --project Mostlylucid --launch-profile https
/// </summary>
public class AnnouncementBannerTests : E2ETestBase
{
    public AnnouncementBannerTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080 with active announcement")]
    public async Task AnnouncementBanner_DisplaysWhenActive()
    {
        // Arrange & Act
        await NavigateAsync("/");

        // Check if announcement banner exists
        var bannerExists = await ElementExistsAsync("#announcement-banner");

        if (!bannerExists)
        {
            Output.WriteLine("⚠️ No active announcement banner found - this is expected if no announcement is configured");
            return;
        }

        // Assert - Banner should be visible
        var bannerVisible = await EvaluateFunctionAsync<bool>(@"() => {
            const wrapper = document.querySelector('#announcement-wrapper');
            if (!wrapper) return false;
            const style = window.getComputedStyle(wrapper);
            return style.display !== 'none' && style.opacity !== '0';
        }");

        Assert.True(bannerVisible, "Announcement banner should be visible when active");
        Output.WriteLine("✅ Announcement banner is displayed");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080 with active announcement")]
    public async Task AnnouncementBanner_HasContent()
    {
        // Arrange
        await NavigateAsync("/");

        var bannerExists = await ElementExistsAsync("#announcement-banner");
        if (!bannerExists)
        {
            Output.WriteLine("⚠️ No active announcement banner found");
            return;
        }

        // Act - Get banner content
        var content = await EvaluateFunctionAsync<string>(@"() => {
            const banner = document.querySelector('#announcement-banner .prose');
            return banner?.innerHTML?.trim() || '';
        }");

        Output.WriteLine($"Banner content: {content}");

        // Assert - Should have some content
        Assert.False(string.IsNullOrWhiteSpace(content), "Announcement banner should have content");
        Output.WriteLine("✅ Announcement banner has content");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080 with active announcement")]
    public async Task AnnouncementBanner_DismissButton_HidesBanner()
    {
        // Arrange
        await NavigateAsync("/");

        var bannerExists = await ElementExistsAsync("#announcement-banner");
        if (!bannerExists)
        {
            Output.WriteLine("⚠️ No active announcement banner found");
            return;
        }

        // Verify banner is initially visible
        var initiallyVisible = await EvaluateFunctionAsync<bool>(@"() => {
            const wrapper = document.querySelector('#announcement-wrapper');
            return wrapper && window.getComputedStyle(wrapper).opacity !== '0';
        }");
        Assert.True(initiallyVisible, "Banner should be visible initially");

        // Act - Click the dismiss button
        await ClickAsync("#announcement-wrapper button[aria-label='Dismiss announcement']");

        // Wait for animation
        await WaitAsync(500);

        // Assert - Banner should be hidden
        var bannerHidden = await EvaluateFunctionAsync<bool>(@"() => {
            const wrapper = document.querySelector('#announcement-wrapper');
            if (!wrapper) return true;
            const style = window.getComputedStyle(wrapper);
            return style.display === 'none' || style.opacity === '0';
        }");

        Assert.True(bannerHidden, "Announcement banner should be hidden after dismiss");
        Output.WriteLine("✅ Dismiss button hides the announcement banner");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080 with active announcement")]
    public async Task AnnouncementBanner_DismissButton_CallsDismissEndpoint()
    {
        // Arrange
        await NavigateAsync("/");

        var bannerExists = await ElementExistsAsync("#announcement-banner");
        if (!bannerExists)
        {
            Output.WriteLine("⚠️ No active announcement banner found");
            return;
        }

        // Set up network request interception
        var dismissRequestMade = false;
        await Page.SetRequestInterceptionAsync(true);

        Page.Request += (sender, e) =>
        {
            if (e.Request.Url.Contains("/announcement/dismiss"))
            {
                dismissRequestMade = true;
                Output.WriteLine($"Dismiss request intercepted: {e.Request.Url}");
            }
            e.Request.ContinueAsync();
        };

        // Act - Click dismiss
        await ClickAsync("#announcement-wrapper button[aria-label='Dismiss announcement']");
        await WaitAsync(500);

        // Assert
        Assert.True(dismissRequestMade, "Dismiss button should make a request to /announcement/dismiss");
        Output.WriteLine("✅ Dismiss button calls dismiss endpoint");

        // Clean up
        await Page.SetRequestInterceptionAsync(false);
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080 with active announcement")]
    public async Task AnnouncementBanner_Animation_SlidesIn()
    {
        // Arrange - Navigate to page and check for animation classes
        await NavigateAsync("/");

        var bannerExists = await ElementExistsAsync("#announcement-banner");
        if (!bannerExists)
        {
            Output.WriteLine("⚠️ No active announcement banner found");
            return;
        }

        // Assert - Check that the wrapper has animation classes
        var hasAnimationClasses = await EvaluateFunctionAsync<bool>(@"() => {
            const wrapper = document.querySelector('#announcement-wrapper');
            if (!wrapper) return false;

            // Check for x-transition attributes (Alpine.js animation)
            const hasEnterTransition = wrapper.hasAttribute('x-transition:enter');
            const hasLeaveTransition = wrapper.hasAttribute('x-transition:leave');

            return hasEnterTransition && hasLeaveTransition;
        }");

        Assert.True(hasAnimationClasses, "Announcement wrapper should have transition animations configured");
        Output.WriteLine("✅ Announcement banner has slide animation configured");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080 with active announcement")]
    public async Task AnnouncementBanner_HasDismissButton()
    {
        // Arrange
        await NavigateAsync("/");

        var bannerExists = await ElementExistsAsync("#announcement-banner");
        if (!bannerExists)
        {
            Output.WriteLine("⚠️ No active announcement banner found");
            return;
        }

        // Assert - Dismiss button should exist with proper attributes
        var dismissButtonExists = await ElementExistsAsync("#announcement-wrapper button[aria-label='Dismiss announcement']");
        Assert.True(dismissButtonExists, "Dismiss button should exist");

        // Check for accessibility
        var hasAriaLabel = await EvaluateFunctionAsync<bool>(@"() => {
            const button = document.querySelector('#announcement-wrapper button');
            return button && button.getAttribute('aria-label') === 'Dismiss announcement';
        }");

        Assert.True(hasAriaLabel, "Dismiss button should have aria-label for accessibility");
        Output.WriteLine("✅ Announcement banner has accessible dismiss button");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080 with active announcement")]
    public async Task AnnouncementBanner_LinksAreClickable()
    {
        // Arrange
        await NavigateAsync("/");

        var bannerExists = await ElementExistsAsync("#announcement-banner");
        if (!bannerExists)
        {
            Output.WriteLine("⚠️ No active announcement banner found");
            return;
        }

        // Check if there are any links in the banner
        var linkCount = await EvaluateFunctionAsync<int>(@"() => {
            const banner = document.querySelector('#announcement-banner .prose');
            return banner ? banner.querySelectorAll('a').length : 0;
        }");

        Output.WriteLine($"Found {linkCount} links in announcement banner");

        if (linkCount == 0)
        {
            Output.WriteLine("⚠️ No links in the current announcement");
            return;
        }

        // Get the first link href
        var firstLinkHref = await EvaluateFunctionAsync<string>(@"() => {
            const link = document.querySelector('#announcement-banner .prose a');
            return link?.getAttribute('href') || '';
        }");

        Assert.False(string.IsNullOrEmpty(firstLinkHref), "Link should have an href");
        Output.WriteLine($"✅ Found clickable link: {firstLinkHref}");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080 with active announcement")]
    public async Task AnnouncementBanner_StylingIsCorrect()
    {
        // Arrange
        await NavigateAsync("/");

        var bannerExists = await ElementExistsAsync("#announcement-banner");
        if (!bannerExists)
        {
            Output.WriteLine("⚠️ No active announcement banner found");
            return;
        }

        // Assert - Check gradient background class is applied
        var hasGradient = await EvaluateFunctionAsync<bool>(@"() => {
            const banner = document.querySelector('#announcement-banner');
            return banner && banner.classList.contains('bg-gradient-to-r');
        }");

        Assert.True(hasGradient, "Banner should have gradient background");

        // Check text is white
        var hasWhiteText = await EvaluateFunctionAsync<bool>(@"() => {
            const banner = document.querySelector('#announcement-banner');
            return banner && banner.classList.contains('text-white');
        }");

        Assert.True(hasWhiteText, "Banner should have white text");

        Output.WriteLine("✅ Announcement banner styling is correct");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080 with active announcement")]
    public async Task AnnouncementBanner_DisplaysOnMultiplePages()
    {
        // Arrange & Act - Check announcement on home page
        await NavigateAsync("/");
        var onHomePage = await ElementExistsAsync("#announcement-banner");

        // Check on blog page
        await NavigateAsync("/blog");
        var onBlogPage = await ElementExistsAsync("#announcement-banner");

        Output.WriteLine($"Banner on home page: {onHomePage}");
        Output.WriteLine($"Banner on blog page: {onBlogPage}");

        // Assert - If announcement exists, it should be on both pages
        if (onHomePage || onBlogPage)
        {
            Assert.True(onHomePage && onBlogPage,
                "If announcement is active, it should appear on both home and blog pages");
            Output.WriteLine("✅ Announcement banner displays consistently across pages");
        }
        else
        {
            Output.WriteLine("⚠️ No active announcement on either page");
        }
    }
}
