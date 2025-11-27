using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PuppeteerSharp;
using Xunit;
using Xunit.Abstractions;

namespace Mostlylucid.Test.Integration;

/// <summary>
/// Integration tests for the markdown pipeline.
/// NOTE: These tests require the site to be running locally and are NOT run in CI.
/// Run the site first with: dotnet run --project Mostlylucid --launch-profile https
/// </summary>
public class MarkdownPipelineIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _markdownPath;
    private readonly string _testFileName;
    private readonly string _testSlug;
    private string? _testFilePath;

    public MarkdownPipelineIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Get the markdown directory path
        var projectRoot = FindProjectRoot();
        _markdownPath = Path.Combine(projectRoot, "Mostlylucid", "Markdown");

        // Generate unique test file name with timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        _testSlug = $"integration-test-{timestamp}";
        _testFileName = $"{_testSlug}.md";
    }

    [Fact(Skip = "Local integration test only - requires site to be running")]
    public async Task MarkdownPipeline_EndToEnd_CreatesAndDeletesPost()
    {
        var browser = await LaunchBrowser();
        var page = await browser.NewPageAsync();

        try
        {
            // Step 1: Create markdown file with all features
            _output.WriteLine("Step 1: Creating test markdown file...");
            CreateTestMarkdownFile();
            _output.WriteLine($"✅ Created: {_testFilePath}");

            // Step 2: Wait for file system watcher to pick it up
            _output.WriteLine("\nStep 2: Waiting for file system watcher to process file...");
            await Task.Delay(5000); // Give file watcher time to process

            // Step 3: Navigate to blog list and verify post exists
            _output.WriteLine("\nStep 3: Navigating to blog list...");
            await page.GoToAsync("http://localhost:8080/blog", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                Timeout = 30000
            });

            // Check if post appears in list
            var postLinkExists = await page.EvaluateFunctionAsync<bool>(@"(slug) => {
                const links = Array.from(document.querySelectorAll('a'));
                return links.some(link => link.href.includes(`/blog/${slug}`));
            }", _testSlug);

            Assert.True(postLinkExists, $"Post with slug '{_testSlug}' should appear in blog list");
            _output.WriteLine($"✅ Post found in blog list");

            // Step 4: Navigate to the post page
            _output.WriteLine("\nStep 4: Navigating to post page...");
            await page.GoToAsync($"http://localhost:8080/blog/{_testSlug}", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                Timeout = 30000
            });

            // Step 5: Verify markdown features are processed correctly
            _output.WriteLine("\nStep 5: Verifying markdown features...");
            await VerifyMarkdownFeatures(page);

            // Step 6: Wait for translations to kick off (check console logs)
            _output.WriteLine("\nStep 6: Waiting for translation service to process...");
            await Task.Delay(10000); // Give translation service time to start

            // Step 7: Delete the markdown file
            _output.WriteLine("\nStep 7: Deleting test markdown file...");
            DeleteTestMarkdownFile();
            _output.WriteLine($"✅ Deleted: {_testFilePath}");

            // Step 8: Wait for file system watcher to pick up deletion
            _output.WriteLine("\nStep 8: Waiting for file system watcher to process deletion...");
            await Task.Delay(5000);

            // Step 9: Verify post no longer appears in blog list
            _output.WriteLine("\nStep 9: Verifying post is removed from blog list...");
            await page.GoToAsync("http://localhost:8080/blog", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                Timeout = 30000
            });

            var postStillExists = await page.EvaluateFunctionAsync<bool>(@"(slug) => {
                const links = Array.from(document.querySelectorAll('a'));
                return links.some(link => link.href.includes(`/blog/${slug}`));
            }", _testSlug);

            Assert.False(postStillExists, $"Post with slug '{_testSlug}' should be removed from blog list");
            _output.WriteLine($"✅ Post successfully removed from blog list");

            // Step 10: Verify post page returns 404
            _output.WriteLine("\nStep 10: Verifying post page returns 404...");
            var response = await page.GoToAsync($"http://localhost:8080/blog/{_testSlug}", new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                Timeout = 30000
            });

            Assert.True(response.Status == System.Net.HttpStatusCode.NotFound, "Post page should return 404");
            _output.WriteLine($"✅ Post page correctly returns 404");

            _output.WriteLine("\n✅ All integration tests passed!");
        }
        finally
        {
            await page.CloseAsync();
            await browser.CloseAsync();
        }
    }

    private void CreateTestMarkdownFile()
    {
        var now = DateTime.Now;
        var testContent = $@"# Integration Test Post - {now:yyyy-MM-dd HH:mm:ss}

<!-- category -- Testing, Integration -->
<datetime class=""hidden"">{now:yyyy-MM-ddTHH:mm}</datetime>

This is an **integration test** post created automatically to test the markdown pipeline.

[TOC]

## Features Being Tested

This post tests the following markdown features:

### 1. Datetime Tag
The hidden datetime tag above should be parsed correctly: `{now:yyyy-MM-ddTHH:mm}`

### 2. Category Tag
Categories should be extracted: Testing, Integration

### 3. Bold Text
This text contains **bold formatting** that should render correctly.

### 4. Table of Contents
The [TOC] tag above should generate a table of contents.

### 5. Code Blocks

```csharp
public class TestClass
{{
    public string Name {{ get; set; }} = ""Test"";

    public void DoSomething()
    {{
        Console.WriteLine(""Hello from integration test!"");
    }}
}}
```

### 6. Lists

Unordered list:
- First item
- Second item
- Third item

Ordered list:
1. Step one
2. Step two
3. Step three

### 7. Links and Emphasis

This is a [test link](https://example.com) and this text is *emphasized*.

### 8. Blockquotes

> This is a blockquote that should be styled correctly.
> It can span multiple lines.

## Test Complete

If you can read this, the markdown pipeline is working correctly!

**Test ID:** {_testSlug}
**Created:** {now:yyyy-MM-dd HH:mm:ss}
";

        _testFilePath = Path.Combine(_markdownPath, _testFileName);
        File.WriteAllText(_testFilePath, testContent);
    }

    private void DeleteTestMarkdownFile()
    {
        if (_testFilePath != null && File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);

            // Also clean up hash file if it exists
            var hashFile = $"{_testFilePath}.hash";
            if (File.Exists(hashFile))
            {
                File.Delete(hashFile);
            }

            // Clean up any translation files
            var translatedDir = Path.Combine(_markdownPath, "translated");
            if (Directory.Exists(translatedDir))
            {
                var translatedFiles = Directory.GetFiles(translatedDir, $"{_testSlug}.*");
                foreach (var file in translatedFiles)
                {
                    File.Delete(file);
                }
            }
        }
    }

    private async Task VerifyMarkdownFeatures(IPage page)
    {
        // Verify title/h1 exists
        var titleExists = await page.EvaluateFunctionAsync<bool>(@"() => {
            const h1 = document.querySelector('h1');
            return h1 && h1.textContent.includes('Integration Test Post');
        }");
        Assert.True(titleExists, "Post title (h1) should exist and contain 'Integration Test Post'");
        _output.WriteLine("  ✅ Title/H1 tag processed correctly");

        // Verify categories
        var categoriesExist = await page.EvaluateFunctionAsync<bool>(@"() => {
            const categories = Array.from(document.querySelectorAll('.badge, .category, [class*=""category""]'));
            const categoryText = categories.map(c => c.textContent.toLowerCase()).join(' ');
            return categoryText.includes('testing') || categoryText.includes('integration');
        }");
        Assert.True(categoriesExist, "Categories should be displayed");
        _output.WriteLine("  ✅ Categories processed correctly");

        // Verify bold text is rendered
        var boldExists = await page.EvaluateFunctionAsync<bool>(@"() => {
            const strong = document.querySelector('strong');
            return strong && strong.textContent.includes('bold formatting');
        }");
        Assert.True(boldExists, "Bold text should be rendered");
        _output.WriteLine("  ✅ Bold text rendered correctly");

        // Verify code block exists
        var codeBlockExists = await page.EvaluateFunctionAsync<bool>(@"() => {
            const codeBlocks = document.querySelectorAll('pre code, pre');
            return codeBlocks.length > 0;
        }");
        Assert.True(codeBlockExists, "Code blocks should be rendered");
        _output.WriteLine("  ✅ Code blocks rendered correctly");

        // Verify lists are rendered
        var listsExist = await page.EvaluateFunctionAsync<bool>(@"() => {
            const ul = document.querySelector('ul');
            const ol = document.querySelector('ol');
            return ul && ol;
        }");
        Assert.True(listsExist, "Both ul and ol lists should be rendered");
        _output.WriteLine("  ✅ Lists rendered correctly");

        // Verify blockquote exists
        var blockquoteExists = await page.EvaluateFunctionAsync<bool>(@"() => {
            const blockquote = document.querySelector('blockquote');
            return blockquote && blockquote.textContent.includes('blockquote');
        }");
        Assert.True(blockquoteExists, "Blockquote should be rendered");
        _output.WriteLine("  ✅ Blockquote rendered correctly");

        // Verify table of contents (if applicable)
        var tocOrH2Exists = await page.EvaluateFunctionAsync<bool>(@"() => {
            const h2 = document.querySelector('h2');
            return h2 !== null;
        }");
        Assert.True(tocOrH2Exists, "Headings should be rendered (for TOC)");
        _output.WriteLine("  ✅ Headings/TOC structure rendered correctly");

        _output.WriteLine("✅ All markdown features verified successfully");
    }

    private async Task<IBrowser> LaunchBrowser()
    {
        _output.WriteLine("Downloading Chromium if needed...");
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        _output.WriteLine("Launching browser...");
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = false, // Set to true for CI, false for debugging
            DefaultViewport = new ViewPortOptions
            {
                Width = 1400,
                Height = 1200
            }
        });

        return browser;
    }

    private string FindProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();

        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir, "Mostlylucid.sln")))
            {
                return currentDir;
            }
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        throw new InvalidOperationException("Could not find project root (Mostlylucid.sln)");
    }

    public void Dispose()
    {
        // Cleanup: Ensure test file is deleted even if test fails
        try
        {
            DeleteTestMarkdownFile();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
