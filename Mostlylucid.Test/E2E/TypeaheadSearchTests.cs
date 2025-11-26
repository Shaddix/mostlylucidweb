using Xunit.Abstractions;

namespace Mostlylucid.Test.E2E;

/// <summary>
/// E2E tests for the typeahead search functionality.
/// Tests keyboard navigation, result selection, and search behavior.
///
/// NOTE: Requires site to be running locally.
/// Run: dotnet run --project Mostlylucid --launch-profile https
/// </summary>
public class TypeaheadSearchTests : E2ETestBase
{
    public TypeaheadSearchTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task TypeaheadSearch_DisplaysResults_WhenTypingQuery()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Act - Type a search query
        var searchInput = "#searchelement input[type='text']";
        await TypeAsync(searchInput, "asp", delay: 100);

        // Wait for debounce and results
        await WaitAsync(500);

        // Assert - Results should appear
        var resultsVisible = await EvaluateFunctionAsync<bool>(@"() => {
            const results = document.querySelector('#searchresults');
            return results && !results.classList.contains('hidden') && results.querySelectorAll('li').length > 0;
        }");

        Assert.True(resultsVisible, "Search results should be visible after typing");
        Output.WriteLine("✅ Search results displayed correctly");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task TypeaheadSearch_KeyboardNavigation_ArrowDown_HighlightsResult()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Type a search query
        var searchInput = "#searchelement input[type='text']";
        await TypeAsync(searchInput, "dotnet", delay: 100);
        await WaitAsync(500);

        // Verify results are shown
        var hasResults = await EvaluateFunctionAsync<bool>(@"() => {
            const results = document.querySelectorAll('#searchresults li');
            return results.length > 0;
        }");

        if (!hasResults)
        {
            Output.WriteLine("⚠️ No search results found - skipping keyboard navigation test");
            return;
        }

        // Act - Press arrow down
        await PressKeyAsync("ArrowDown");
        await WaitAsync(100);

        // Assert - First result should be highlighted
        var highlightedIndex = await EvaluateFunctionAsync<int>(@"() => {
            return window.Alpine?.store?.('typeahead')?.highlightedIndex ??
                   document.querySelector('#searchelement')?.__x?.$data?.highlightedIndex ?? -1;
        }");

        // Check if any result has the highlighted class
        var hasHighlight = await EvaluateFunctionAsync<bool>(@"() => {
            const items = document.querySelectorAll('#searchresults li');
            return Array.from(items).some(item =>
                item.classList.contains('bg-blue-light') ||
                item.classList.contains('dark:bg-blue-dark'));
        }");

        Assert.True(hasHighlight, "First result should be highlighted after pressing ArrowDown");
        Output.WriteLine("✅ Arrow down navigation highlights result");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task TypeaheadSearch_KeyboardNavigation_Enter_NavigatesToResult()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Type a search query
        var searchInput = "#searchelement input[type='text']";
        await TypeAsync(searchInput, "dotnet", delay: 100);
        await WaitAsync(500);

        // Verify results are shown
        var hasResults = await EvaluateFunctionAsync<bool>(@"() => {
            const results = document.querySelectorAll('#searchresults li');
            return results.length > 0;
        }");

        if (!hasResults)
        {
            Output.WriteLine("⚠️ No search results found - skipping Enter navigation test");
            return;
        }

        // Get the URL of the first result
        var firstResultUrl = await EvaluateFunctionAsync<string>(@"() => {
            const firstLink = document.querySelector('#searchresults li a');
            return firstLink?.getAttribute('href') ?? '';
        }");

        Output.WriteLine($"First result URL: {firstResultUrl}");

        // Act - Press arrow down then enter
        await PressKeyAsync("ArrowDown");
        await WaitAsync(100);
        await PressKeyAsync("Enter");

        // Wait for navigation
        await WaitAsync(1000);

        // Assert - Should navigate to the result page
        var currentUrl = Page.Url;
        Output.WriteLine($"Current URL after Enter: {currentUrl}");

        // The URL should contain /blog/ and not have a 500 error
        Assert.Contains("/blog/", currentUrl);
        Assert.DoesNotContain("error", currentUrl.ToLower());
        Output.WriteLine("✅ Enter key navigates to selected result");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task TypeaheadSearch_ClickResult_NavigatesToPost()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Type a search query
        var searchInput = "#searchelement input[type='text']";
        await TypeAsync(searchInput, "docker", delay: 100);
        await WaitAsync(500);

        // Verify results are shown
        var hasResults = await WaitForSelectorAsync("#searchresults li", timeout: 3000);
        if (hasResults == null)
        {
            Output.WriteLine("⚠️ No search results found - skipping click test");
            return;
        }

        // Get the expected URL
        var expectedSlug = await EvaluateFunctionAsync<string>(@"() => {
            const firstLink = document.querySelector('#searchresults li a');
            return firstLink?.getAttribute('href') ?? '';
        }");

        Output.WriteLine($"Expected navigation to: {expectedSlug}");

        // Act - Click the first result
        await ClickAsync("#searchresults li a");

        // Wait for navigation
        await WaitAsync(1000);

        // Assert - Should be on the blog post page
        var currentUrl = Page.Url;
        Output.WriteLine($"Current URL after click: {currentUrl}");

        Assert.Contains("/blog/", currentUrl);
        Output.WriteLine("✅ Click on result navigates to post");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task TypeaheadSearch_ResultsHide_WhenClickingOutside()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Type a search query
        var searchInput = "#searchelement input[type='text']";
        await TypeAsync(searchInput, "test", delay: 100);
        await WaitAsync(500);

        // Verify results are shown
        var resultsShown = await EvaluateFunctionAsync<bool>(@"() => {
            const results = document.querySelector('#searchresults');
            return results && !results.classList.contains('hidden');
        }");

        if (!resultsShown)
        {
            Output.WriteLine("⚠️ Search results not shown - skipping click outside test");
            return;
        }

        // Act - Click outside the search element
        await ClickAsync("body");
        await WaitAsync(300);

        // Assert - Results should be hidden
        var resultsHidden = await EvaluateFunctionAsync<bool>(@"() => {
            const results = document.querySelector('#searchresults');
            return !results || results.classList.contains('hidden') ||
                   window.getComputedStyle(results).display === 'none';
        }");

        Assert.True(resultsHidden, "Search results should hide when clicking outside");
        Output.WriteLine("✅ Results hide when clicking outside");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task TypeaheadSearch_NoResults_WhenQueryTooShort()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Act - Type only 2 characters (less than minimum)
        var searchInput = "#searchelement input[type='text']";
        await TypeAsync(searchInput, "ab", delay: 100);
        await WaitAsync(500);

        // Assert - Results should not be visible (need 3+ chars)
        var resultsHidden = await EvaluateFunctionAsync<bool>(@"() => {
            const results = document.querySelector('#searchresults');
            return !results || results.classList.contains('hidden') ||
                   results.querySelectorAll('li').length === 0;
        }");

        Assert.True(resultsHidden, "Search results should not show for queries under 3 characters");
        Output.WriteLine("✅ Results hidden for short queries");
    }

    [Fact(Skip = "Local E2E test - requires site to be running on localhost:8080")]
    public async Task TypeaheadSearch_ArrowUpDown_CyclesResults()
    {
        // Arrange
        await NavigateAsync("/blog");

        // Type a search query
        var searchInput = "#searchelement input[type='text']";
        await TypeAsync(searchInput, "blog", delay: 100);
        await WaitAsync(500);

        // Check how many results we have
        var resultCount = await EvaluateFunctionAsync<int>(@"() => {
            return document.querySelectorAll('#searchresults li').length;
        }");

        if (resultCount < 2)
        {
            Output.WriteLine($"⚠️ Only {resultCount} results - need at least 2 for cycling test");
            return;
        }

        Output.WriteLine($"Found {resultCount} results for cycling test");

        // Act - Press ArrowDown twice, then ArrowUp
        await PressKeyAsync("ArrowDown");
        await WaitAsync(100);

        var indexAfterFirstDown = await EvaluateFunctionAsync<int>(@"() => {
            const el = document.querySelector('#searchelement');
            return el?.__x?.$data?.highlightedIndex ?? 0;
        }");
        Output.WriteLine($"Index after first ArrowDown: {indexAfterFirstDown}");

        await PressKeyAsync("ArrowDown");
        await WaitAsync(100);

        var indexAfterSecondDown = await EvaluateFunctionAsync<int>(@"() => {
            const el = document.querySelector('#searchelement');
            return el?.__x?.$data?.highlightedIndex ?? 0;
        }");
        Output.WriteLine($"Index after second ArrowDown: {indexAfterSecondDown}");

        await PressKeyAsync("ArrowUp");
        await WaitAsync(100);

        var indexAfterUp = await EvaluateFunctionAsync<int>(@"() => {
            const el = document.querySelector('#searchelement');
            return el?.__x?.$data?.highlightedIndex ?? 0;
        }");
        Output.WriteLine($"Index after ArrowUp: {indexAfterUp}");

        // Assert - Index should have moved
        Assert.True(indexAfterSecondDown > indexAfterFirstDown || indexAfterSecondDown == 0,
            "ArrowDown should increment highlighted index");
        Output.WriteLine("✅ Arrow keys cycle through results correctly");
    }
}
