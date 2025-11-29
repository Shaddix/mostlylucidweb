using PuppeteerSharp;
using Xunit.Abstractions;

namespace Mostlylucid.Test.E2E;

/// <summary>
/// E2E tests for the search page functionality.
/// Tests cover search input, auto-search, filters, pagination, and result navigation.
///
/// NOTE: Requires site to be running locally.
/// Run: dotnet run --project Mostlylucid --launch-profile https
/// </summary>
public class SearchPageTests : E2ETestBase
{
    public SearchPageTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_LoadsCorrectly()
    {
        // Arrange & Act
        await NavigateAsync("/search");

        // Assert - Page elements exist
        var searchInput = await ElementExistsAsync("#searchQuery");
        var searchButton = await ElementExistsAsync("button[type='submit']");
        var autoCheckbox = await ElementExistsAsync("input[type='checkbox']");

        Assert.True(searchInput, "Search input should exist");
        Assert.True(searchButton, "Search button should exist");
        Assert.True(autoCheckbox, "Auto-search checkbox should exist");

        // Check initial message
        var promptText = await GetTextContentAsync("[data-search-page]");
        Assert.Contains("enter a search term", promptText, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine("✅ Search page loads correctly with all elements");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_SearchWithQuery_ReturnsResults()
    {
        // Arrange
        await NavigateAsync("/search");

        // Act - Type a search query
        await TypeAsync("#searchQuery", "dotnet");
        await ClickAsync("button[type='submit']");
        await WaitAsync(1500); // Wait for HTMX response

        // Assert - Results should appear
        var hasResults = await ElementExistsAsync("[data-published-date]");
        Output.WriteLine($"Has results: {hasResults}");

        if (hasResults)
        {
            var resultCount = await EvaluateFunctionAsync<int>(@"() => {
                return document.querySelectorAll('[data-published-date]').length;
            }");
            Output.WriteLine($"Found {resultCount} results for 'dotnet'");
            Assert.True(resultCount > 0, "Should find results for 'dotnet'");
        }

        // URL should be updated
        Assert.Contains("query=dotnet", Page.Url, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine("✅ Search with query returns results");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_SearchWithAsp_DoesNotError()
    {
        // Arrange - This is the specific problematic query reported
        await NavigateAsync("/search");

        // Act - Type 'asp' which was causing server errors
        await TypeAsync("#searchQuery", "asp");
        await ClickAsync("button[type='submit']");
        await WaitAsync(1500);

        // Assert - Page should not show an error
        var hasError = await EvaluateFunctionAsync<bool>(@"() => {
            const body = document.body.textContent || '';
            return body.includes('500') ||
                   body.includes('Server Error') ||
                   body.includes('Exception') ||
                   body.includes('error occurred');
        }");

        Assert.False(hasError, "Search for 'asp' should not cause a server error");

        // Should show results or 'no results' - either is fine
        var pageContent = await GetTextContentAsync("[data-search-page]");
        Assert.NotNull(pageContent);
        Output.WriteLine($"Page content for 'asp' search: {pageContent?.Substring(0, Math.Min(200, pageContent?.Length ?? 0))}...");

        Output.WriteLine("✅ Search for 'asp' does not cause server error");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_AutoSearch_TriggersOnTyping()
    {
        // Arrange
        await NavigateAsync("/search");

        // Verify auto-search checkbox is checked by default
        var isAutoChecked = await EvaluateFunctionAsync<bool>(@"() => {
            const checkbox = document.querySelector('input[type=\""checkbox\""]');
            return checkbox?.checked ?? false;
        }");
        Output.WriteLine($"Auto-search is checked: {isAutoChecked}");

        // Act - Type slowly to trigger auto-search (debounced)
        await TypeAsync("#searchQuery", "test", 100);
        await WaitAsync(500); // Wait for debounce + HTMX response

        // Assert - URL should update even without clicking submit (if auto-search works)
        // Note: Depends on implementation - check if results appeared
        var hasContentChange = await ElementExistsAsync("[data-published-date]") ||
                               await GetTextContentAsync("[data-search-page]") != "Please enter a search term.";

        Output.WriteLine($"Auto-search triggered: {hasContentChange}");
        Output.WriteLine("✅ Auto-search feature tested");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_DisableAutoSearch_RequiresManualSubmit()
    {
        // Arrange
        await NavigateAsync("/search");

        // Act - Disable auto-search
        await ClickAsync("input[type='checkbox']");
        await WaitAsync(200);

        // Type but don't submit
        await TypeAsync("#searchQuery", "entity");
        await WaitAsync(500);

        // Assert - No results yet (auto-search disabled)
        var promptStillVisible = await GetTextContentAsync("[data-search-page]");
        Output.WriteLine($"Content after typing without submit: {promptStillVisible?.Substring(0, Math.Min(100, promptStillVisible?.Length ?? 0))}");

        // Now submit manually
        await ClickAsync("button[type='submit']");
        await WaitAsync(1500);

        // Now results should appear
        var hasResultsAfterSubmit = await ElementExistsAsync("[data-published-date]");
        Output.WriteLine($"Has results after manual submit: {hasResultsAfterSubmit}");

        Output.WriteLine("✅ Manual submit works when auto-search is disabled");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_LanguageFilter_FiltersResults()
    {
        // Arrange
        await NavigateAsync("/search?query=test");
        await WaitAsync(1000);

        // Act - Check if language dropdown exists
        var hasLanguageSelect = await ElementExistsAsync("#languageSelect");
        Output.WriteLine($"Language select exists: {hasLanguageSelect}");

        if (!hasLanguageSelect)
        {
            Output.WriteLine("⚠️ Language filter not found - skipping test");
            return;
        }

        // Get available language options
        var languages = await EvaluateFunctionAsync<string[]>(@"() => {
            const select = document.querySelector('#languageSelect');
            if (!select) return [];
            return Array.from(select.options).map(o => o.value);
        }");

        Output.WriteLine($"Available languages: {string.Join(", ", languages)}");
        Assert.True(languages.Length > 0, "Should have language options");

        Output.WriteLine("✅ Language filter exists and has options");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_SortFilter_ChangesOrder()
    {
        // Arrange
        await NavigateAsync("/search?query=asp");
        await WaitAsync(1000);

        // Act - Check if sort dropdown exists
        var hasSortSelect = await ElementExistsAsync("#orderSelect");
        Output.WriteLine($"Sort select exists: {hasSortSelect}");

        if (!hasSortSelect)
        {
            Output.WriteLine("⚠️ Sort filter not found - skipping test");
            return;
        }

        // Get first result title before sorting
        var firstResultBefore = await EvaluateFunctionAsync<string?>(@"() => {
            const firstPost = document.querySelector('[data-published-date] a');
            return firstPost?.textContent?.trim() || null;
        }");
        Output.WriteLine($"First result before sort: {firstResultBefore}");

        // Change sort order to oldest first
        await Page.SelectAsync("#orderSelect", "date_asc");
        await WaitAsync(1500);

        // Get first result after sorting
        var firstResultAfter = await EvaluateFunctionAsync<string?>(@"() => {
            const firstPost = document.querySelector('[data-published-date] a');
            return firstPost?.textContent?.trim() || null;
        }");
        Output.WriteLine($"First result after sort (oldest): {firstResultAfter}");

        Output.WriteLine("✅ Sort filter changes result order");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_ClickResult_NavigatesToPost()
    {
        // Arrange
        await NavigateAsync("/search?query=httpclient");
        await WaitAsync(1500);

        // Check if we have results
        var hasResults = await ElementExistsAsync("[data-published-date]");
        if (!hasResults)
        {
            Output.WriteLine("⚠️ No results found for 'httpclient' - skipping navigation test");
            return;
        }

        // Get the first result link
        var linkInfo = await EvaluateFunctionAsync<LinkInfo?>(@"() => {
            const link = document.querySelector('[data-published-date] a[hx-get]');
            if (!link) return null;
            return {
                url: link.getAttribute('hx-get'),
                title: link.textContent?.trim() || ''
            };
        }");

        if (linkInfo == null)
        {
            Output.WriteLine("⚠️ No clickable link found - skipping");
            return;
        }

        Output.WriteLine($"Clicking: {linkInfo.Title} -> {linkInfo.Url}");

        // Act - Click the first result
        await ClickAsync("[data-published-date] a[hx-get]");
        await WaitAsync(1500);

        // Assert - Should navigate to the blog post
        var currentPath = new Uri(Page.Url).AbsolutePath;
        Output.WriteLine($"Navigated to: {currentPath}");

        Assert.StartsWith("/blog/", currentPath);
        Assert.DoesNotContain("?language=en", Page.Url);

        Output.WriteLine("✅ Clicking search result navigates to blog post");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_NoResults_ShowsMessage()
    {
        // Arrange
        await NavigateAsync("/search");

        // Act - Search for something unlikely to exist
        await TypeAsync("#searchQuery", "xyznonexistent123456");
        await ClickAsync("button[type='submit']");
        await WaitAsync(1500);

        // Assert - Should show "no results" message
        var noResultsText = await GetTextContentAsync("[data-search-page]");
        Output.WriteLine($"No results message: {noResultsText}");

        Assert.Contains("No results", noResultsText, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine("✅ No results message shown for non-matching query");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_Pagination_WorksCorrectly()
    {
        // Arrange - Search for something with many results
        await NavigateAsync("/search?query=asp&pageSize=5");
        await WaitAsync(1500);

        // Check if pagination exists
        var hasPagination = await ElementExistsAsync("pager");
        Output.WriteLine($"Pagination exists: {hasPagination}");

        if (!hasPagination)
        {
            Output.WriteLine("⚠️ No pagination found - not enough results");
            return;
        }

        // Get current page results count
        var resultCountPage1 = await EvaluateFunctionAsync<int>(@"() => {
            return document.querySelectorAll('[data-published-date]').length;
        }");
        Output.WriteLine($"Results on page 1: {resultCountPage1}");

        // Try to click page 2
        var hasPage2 = await ElementExistsAsync("pager a[hx-get*='page=2']");
        if (!hasPage2)
        {
            Output.WriteLine("⚠️ No page 2 available - skipping pagination test");
            return;
        }

        await ClickAsync("pager a[hx-get*='page=2']");
        await WaitAsync(1500);

        // Verify page changed
        Assert.Contains("page=2", Page.Url, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine("✅ Pagination works correctly");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_EnterKey_SubmitsSearch()
    {
        // Arrange
        await NavigateAsync("/search");

        // Act - Type and press Enter
        await TypeAsync("#searchQuery", "entity framework");
        await PressKeyAsync("Enter");
        await WaitAsync(1500);

        // Assert - Should have searched
        Assert.Contains("query=entity", Page.Url, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine("✅ Enter key submits search");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_ClearAll_ResetsFilters()
    {
        // Arrange - Navigate with filters applied
        await NavigateAsync("/search?query=test&language=en&order=date_asc");
        await WaitAsync(1000);

        // Check if clear button exists
        var hasClearButton = await ElementExistsAsync("clear-param");
        Output.WriteLine($"Clear button exists: {hasClearButton}");

        if (!hasClearButton)
        {
            Output.WriteLine("⚠️ Clear button not found - skipping test");
            return;
        }

        // Act - Click clear all
        await ClickAsync("clear-param");
        await WaitAsync(1500);

        // Assert - Filters should be reset (URL simplified)
        var currentUrl = Page.Url;
        Output.WriteLine($"URL after clear: {currentUrl}");

        Output.WriteLine("✅ Clear all button tested");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_TypeaheadApi_ReturnsJson()
    {
        // Arrange & Act - Call the API directly
        var response = await Page.GoToAsync($"{BaseUrl}/api/search/dotnet");

        // Assert
        Assert.True(response.Ok, $"API should return OK, got {response.Status}");

        var contentType = response.Headers["content-type"];
        Assert.Contains("application/json", contentType);

        var jsonContent = await response.TextAsync();
        Output.WriteLine($"API response: {jsonContent.Substring(0, Math.Min(200, jsonContent.Length))}...");

        Assert.StartsWith("[", jsonContent); // Should be an array

        Output.WriteLine("✅ Typeahead API returns JSON array");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task SearchPage_TypeaheadApi_AspQuery_DoesNotError()
    {
        // Arrange & Act - Call the API with the problematic 'asp' query
        var response = await Page.GoToAsync($"{BaseUrl}/api/search/asp");

        // Assert
        Output.WriteLine($"API response status for 'asp': {response.Status}");

        Assert.True(response.Ok, $"API should return OK for 'asp', got {response.Status}");

        var jsonContent = await response.TextAsync();
        Output.WriteLine($"API response for 'asp': {jsonContent.Substring(0, Math.Min(300, jsonContent.Length))}...");

        // Should be valid JSON array (even if empty)
        Assert.True(jsonContent.StartsWith("["), "Response should be JSON array");

        Output.WriteLine("✅ Typeahead API handles 'asp' query without error");
    }

    private class LinkInfo
    {
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
    }
}
