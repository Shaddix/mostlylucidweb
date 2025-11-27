using PuppeteerSharp;
using PuppeteerSharp.Input;
using Xunit.Abstractions;

namespace Mostlylucid.Test.E2E;

/// <summary>
/// Base class for E2E tests using PuppeteerSharp.
/// NOTE: These tests require the site to be running locally.
/// Run the site first with: dotnet run --project Mostlylucid --launch-profile https
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly ITestOutputHelper Output;
    protected IBrowser Browser = null!;
    protected IPage Page = null!;

    protected const string BaseUrl = "http://localhost:8080";
    protected const int DefaultTimeout = 30000;

    protected E2ETestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    public async Task InitializeAsync()
    {
        Output.WriteLine("Downloading Chromium if needed...");
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        Output.WriteLine("Launching browser...");
        Browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true, // Set to false for debugging
            DefaultViewport = new ViewPortOptions
            {
                Width = 1400,
                Height = 900
            },
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox"
            }
        });

        Page = await Browser.NewPageAsync();

        // Set default timeout
        Page.DefaultTimeout = DefaultTimeout;

        Output.WriteLine("Browser ready");
    }

    public async Task DisposeAsync()
    {
        if (Page != null)
        {
            await Page.CloseAsync();
        }

        if (Browser != null)
        {
            await Browser.CloseAsync();
        }
    }

    /// <summary>
    /// Navigate to a page and wait for it to load
    /// </summary>
    protected async Task NavigateAsync(string path)
    {
        var url = path.StartsWith("http") ? path : $"{BaseUrl}{path}";
        Output.WriteLine($"Navigating to: {url}");

        await Page.GoToAsync(url, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
            Timeout = DefaultTimeout
        });
    }

    /// <summary>
    /// Wait for an element to appear
    /// </summary>
    protected async Task<IElementHandle?> WaitForSelectorAsync(string selector, int timeout = 5000)
    {
        try
        {
            return await Page.WaitForSelectorAsync(selector, new WaitForSelectorOptions
            {
                Timeout = timeout,
                Visible = true
            });
        }
        catch (WaitTaskTimeoutException)
        {
            return null;
        }
    }

    /// <summary>
    /// Check if an element exists on the page
    /// </summary>
    protected async Task<bool> ElementExistsAsync(string selector)
    {
        var element = await Page.QuerySelectorAsync(selector);
        return element != null;
    }

    /// <summary>
    /// Get text content of an element
    /// </summary>
    protected async Task<string?> GetTextContentAsync(string selector)
    {
        var element = await Page.QuerySelectorAsync(selector);
        if (element == null) return null;

        return await Page.EvaluateFunctionAsync<string>("el => el.textContent", element);
    }

    /// <summary>
    /// Type text into an input element
    /// </summary>
    protected async Task TypeAsync(string selector, string text, int delay = 50)
    {
        await Page.WaitForSelectorAsync(selector);
        await Page.TypeAsync(selector, text, new TypeOptions { Delay = delay });
    }

    /// <summary>
    /// Click an element
    /// </summary>
    protected async Task ClickAsync(string selector)
    {
        await Page.WaitForSelectorAsync(selector);
        await Page.ClickAsync(selector);
    }

    /// <summary>
    /// Press a key
    /// </summary>
    protected async Task PressKeyAsync(string key)
    {
        await Page.Keyboard.PressAsync(key);
    }

    /// <summary>
    /// Wait for a short delay (useful for animations/debounce)
    /// </summary>
    protected async Task WaitAsync(int milliseconds = 500)
    {
        await Task.Delay(milliseconds);
    }

    /// <summary>
    /// Execute JavaScript in the page context
    /// </summary>
    protected async Task<T> EvaluateAsync<T>(string script)
    {
        return await Page.EvaluateExpressionAsync<T>(script);
    }

    /// <summary>
    /// Execute JavaScript function in the page context
    /// </summary>
    protected async Task<T> EvaluateFunctionAsync<T>(string function, params object[] args)
    {
        return await Page.EvaluateFunctionAsync<T>(function, args);
    }
}
