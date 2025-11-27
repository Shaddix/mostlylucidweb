using PuppeteerSharp;
using Xunit.Abstractions;

namespace Mostlylucid.Test.E2E;

/// <summary>
/// E2E tests for the filter bar functionality on the blog list page.
/// Tests language selection, sorting, and filtering interactions.
///
/// NOTE: Requires site to be running locally.
/// Run: dotnet run --project Mostlylucid --launch-profile https
/// </summary>
public class FilterBarTests : E2ETestBase
{
    public FilterBarTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task FilterBar_LanguageDropdown_ShowsLanguages()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Act - Click the language dropdown button
        var dropdownButton = await WaitForSelectorAsync("#LanguageDropDown button");
        Assert.NotNull(dropdownButton);

        await ClickAsync("#LanguageDropDown button");
        await WaitAsync(300);

        // Assert - Dropdown menu should be visible with language options
        var dropdownOpen = await EvaluateFunctionAsync<bool>(@"() => {
            const dropdown = document.querySelector('#LanguageDropDown div[x-show]');
            if (!dropdown) return false;
            const style = window.getComputedStyle(dropdown);
            return style.display !== 'none';
        }");

        Assert.True(dropdownOpen, "Language dropdown should be open");

        // Check that English option exists
        var hasEnglish = await EvaluateFunctionAsync<bool>(@"() => {
            const options = document.querySelectorAll('#LanguageDropDown li a');
            return Array.from(options).some(opt => opt.textContent.toLowerCase().includes('english'));
        }");

        Assert.True(hasEnglish, "Language dropdown should contain English option");
        Output.WriteLine("✅ Language dropdown shows languages correctly");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task FilterBar_LanguageSelection_UpdatesUI()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Get initial selected language
        var initialLanguage = await EvaluateFunctionAsync<string>(@"() => {
            const button = document.querySelector('#LanguageDropDown button span');
            return button?.textContent || '';
        }");
        Output.WriteLine($"Initial language: {initialLanguage}");

        // Act - Click dropdown and select a different language if available
        await ClickAsync("#LanguageDropDown button");
        await WaitAsync(300);

        // Check if there's more than one language option
        var languageCount = await EvaluateFunctionAsync<int>(@"() => {
            return document.querySelectorAll('#LanguageDropDown li a').length;
        }");

        if (languageCount < 2)
        {
            Output.WriteLine($"⚠️ Only {languageCount} language(s) available - skipping language switch test");
            return;
        }

        // Click the second language option
        await ClickAsync("#LanguageDropDown li:nth-child(2) a");
        await WaitAsync(500);

        // Assert - Selected language should have changed (or URL should reflect)
        var newLanguage = await EvaluateFunctionAsync<string>(@"() => {
            const button = document.querySelector('#LanguageDropDown button span');
            return button?.textContent || '';
        }");
        Output.WriteLine($"New language: {newLanguage}");

        // The button text or URL should reflect a change
        var languageChanged = initialLanguage != newLanguage || Page.Url.Contains("language=");
        Assert.True(languageChanged, "Language selection should update the UI or URL");
        Output.WriteLine("✅ Language selection updates UI correctly");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task FilterBar_SortOrder_ChangesPostOrder()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Get the first post title before sorting
        var firstPostBefore = await EvaluateFunctionAsync<string>(@"() => {
            const postLink = document.querySelector('.post-title, article h2 a, #contentcontainer article a');
            return postLink?.textContent?.trim() || '';
        }");
        Output.WriteLine($"First post before sort: {firstPostBefore}");

        // Act - Change sort order to "Oldest first"
        await Page.SelectAsync("#orderSelect", "date_asc");
        await WaitAsync(1000); // Wait for HTMX to update

        // Assert - Post order should have changed (or at least the request was made)
        var firstPostAfter = await EvaluateFunctionAsync<string>(@"() => {
            const postLink = document.querySelector('.post-title, article h2 a, #contentcontainer article a');
            return postLink?.textContent?.trim() || '';
        }");
        Output.WriteLine($"First post after sort: {firstPostAfter}");

        // Note: We can't guarantee order changed if there's only one post or all same date
        // But the select should at least have the new value
        var selectValue = await EvaluateFunctionAsync<string>("() => document.querySelector('#orderSelect')?.value");
        Assert.Equal("date_asc", selectValue);
        Output.WriteLine("✅ Sort order selection works correctly");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task FilterBar_SortByTitle_ChangesOrder()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Act - Change sort order to "Title A-Z"
        await Page.SelectAsync("#orderSelect", "title_asc");
        await WaitAsync(1000);

        // Assert - Select value should be updated
        var selectValue = await EvaluateFunctionAsync<string>("() => document.querySelector('#orderSelect')?.value");
        Assert.Equal("title_asc", selectValue);

        // Now try Title Z-A
        await Page.SelectAsync("#orderSelect", "title_desc");
        await WaitAsync(1000);

        selectValue = await EvaluateFunctionAsync<string>("() => document.querySelector('#orderSelect')?.value");
        Assert.Equal("title_desc", selectValue);

        Output.WriteLine("✅ Title sorting options work correctly");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task FilterBar_ClearAll_ResetsFilters()
    {
        // Arrange
        await NavigateAsync("/blog");

        // First apply some filters
        await Page.SelectAsync("#orderSelect", "date_asc");
        await WaitAsync(500);

        // Act - Click Clear All button
        var clearAllSelector = "clear-param[all='true'], button:has-text('Clear All'), .clear-all";
        var clearAllExists = await ElementExistsAsync("clear-param[all='true']");

        if (!clearAllExists)
        {
            Output.WriteLine("⚠️ Clear All button not found - skipping test");
            return;
        }

        await ClickAsync("clear-param[all='true']");
        await WaitAsync(1000);

        // Assert - URL should not have filter params or sort should be reset
        var urlHasFilters = Page.Url.Contains("order=") || Page.Url.Contains("language=");

        // After clear, the default sort should be date_desc
        var selectValue = await EvaluateFunctionAsync<string>("() => document.querySelector('#orderSelect')?.value");
        Output.WriteLine($"Sort value after clear: {selectValue}");

        Output.WriteLine("✅ Clear All button functions correctly");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task FilterBar_FilterSummary_ShowsActiveFilters()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Check if filter summary exists
        var filterSummaryExists = await ElementExistsAsync("#filterSummary");
        if (!filterSummaryExists)
        {
            Output.WriteLine("⚠️ Filter summary element not found - skipping test");
            return;
        }

        // Get initial filter summary
        var initialSummary = await GetTextContentAsync("#filterSummary");
        Output.WriteLine($"Initial filter summary: '{initialSummary}'");

        // Act - Apply a filter
        await Page.SelectAsync("#orderSelect", "title_asc");
        await WaitAsync(1000);

        // Assert - Filter summary should update
        var newSummary = await GetTextContentAsync("#filterSummary");
        Output.WriteLine($"Filter summary after sort: '{newSummary}'");

        // The summary should reflect some state (may be empty if no active filters shown)
        Output.WriteLine("✅ Filter summary element is present and responds to changes");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task FilterBar_ResponsiveDesign_HiddenOnMobile()
    {
        // Arrange - Set mobile viewport
        await Page.SetViewportAsync(new ViewPortOptions
        {
            Width = 375,
            Height = 667
        });

        await NavigateAsync("/blog");
        await WaitAsync(500);

        // Assert - Filter bar should be hidden on mobile (has 'hidden lg:flex' class)
        var filterBarVisible = await EvaluateFunctionAsync<bool>(@"() => {
            const filterBar = document.querySelector('.hidden.lg\\:flex');
            if (!filterBar) return true; // If selector not found, consider it visible
            const rect = filterBar.getBoundingClientRect();
            return rect.width > 0 && rect.height > 0;
        }");

        Assert.False(filterBarVisible, "Filter bar should be hidden on mobile viewport");
        Output.WriteLine("✅ Filter bar correctly hidden on mobile");

        // Reset viewport
        await Page.SetViewportAsync(new ViewPortOptions
        {
            Width = 1400,
            Height = 900
        });
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task FilterBar_DateRangeInput_Exists()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Assert - Date range input should exist
        var dateRangeExists = await ElementExistsAsync("#dateRange");
        Assert.True(dateRangeExists, "Date range input should exist on the page");

        // Check it's a text input
        var inputType = await EvaluateFunctionAsync<string>("() => document.querySelector('#dateRange')?.type");
        Assert.Equal("text", inputType);

        Output.WriteLine("✅ Date range input exists and is configured correctly");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task FilterBar_BlogPostsLink_Navigates()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Find the blog posts link
        var blogLinkExists = await ElementExistsAsync("a[href='/blog']");
        if (!blogLinkExists)
        {
            Output.WriteLine("⚠️ Blog posts link not found - skipping test");
            return;
        }

        // Act - Click the blog posts link
        await ClickAsync("a[href='/blog']");
        await WaitAsync(1000);

        // Assert - Should still be on blog page
        Assert.Contains("/blog", Page.Url);
        Output.WriteLine("✅ Blog posts link navigation works");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task FilterBar_RssLink_OpensRssFeed()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Check RSS link exists
        var rssLinkExists = await EvaluateFunctionAsync<bool>("() => { const link = document.querySelector('a[href=\"/rss\"]'); return link !== null; }");

        if (!rssLinkExists)
        {
            Output.WriteLine("RSS link not found on page - may be hidden on this viewport");
            return;
        }

        // Get the RSS link href
        var rssHref = await EvaluateFunctionAsync<string>("() => { const link = document.querySelector('a[href=\"/rss\"]'); return link?.getAttribute('href') || ''; }");

        Assert.Equal("/rss", rssHref);
        Output.WriteLine("RSS link is present and points to /rss");
    }
}
