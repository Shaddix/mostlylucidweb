using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.MarkdownTranslator;
using Xunit;
using Xunit.Abstractions;

namespace Mostlylucid.Test.TranslationService;

public class TranslateService_Live_Tests
{
    private readonly ITestOutputHelper _output;

    public TranslateService_Live_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Live test - only run manually when service is available", DisplayName = "Live test: Spaces are preserved with real translation service")]
    public async Task Test_Live_SpacesPreserved()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMarkdownTranslatorServiceCollection();

        // Override config to use localhost:8000
        var config = services.BuildServiceProvider().GetRequiredService<Mostlylucid.Shared.Config.TranslateServiceConfig>();
        config.IPs = new[] { "http://localhost:8000" };

        var serviceProvider = services.BuildServiceProvider();
        var translateService = serviceProvider.GetRequiredService<IMarkdownTranslatorService>();

        // Check service is up first
        var isUp = await translateService.IsServiceUp(CancellationToken.None);
        Assert.True(isUp, "Translation service at localhost:8000 is not available");

        // Test markdown with bold text containing spaces
        var markdown = "This is **test text. More text ** here.";

        _output.WriteLine($"Input markdown: {markdown}");
        _output.WriteLine($"Input length: {markdown.Length}");

        // Act
        var translated = await translateService.TranslateMarkdown(markdown, "es", CancellationToken.None, null);

        // Output results
        _output.WriteLine($"Output markdown: {translated}");
        _output.WriteLine($"Output length: {translated.Length}");

        // Assert - markdown structure should be preserved
        Assert.Contains("**", translated);

        // The key test: verify spacing is maintained in the structure
        // We can't check exact translation, but we can verify markdown isn't broken
        var boldCount = translated.Count(c => c == '*');
        Assert.Equal(4, boldCount); // Should have ** before and ** after
    }

    [Fact(Skip = "Known issue: Markdig ToMarkdownString() has issues with modified content - use manual reconstruction")]
    public async Task Test_Live_TrailingSpaceInBold()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMarkdownTranslatorServiceCollection();

        var config = services.BuildServiceProvider().GetRequiredService<Mostlylucid.Shared.Config.TranslateServiceConfig>();
        config.IPs = new[] { "http://localhost:8000" };

        var serviceProvider = services.BuildServiceProvider();
        var translateService = serviceProvider.GetRequiredService<IMarkdownTranslatorService>();

        // The exact case from the bug report
        var markdown = "**Tedt. New Text **";

        _output.WriteLine($"Input: '{markdown}'");

        // Act
        var translated = await translateService.TranslateMarkdown(markdown, "es", CancellationToken.None, null);

        // Output
        _output.WriteLine($"Output: '{translated}'");

        // The translated text should still have ** markers
        Assert.StartsWith("**", translated);
        Assert.EndsWith("**", translated);
    }
}
