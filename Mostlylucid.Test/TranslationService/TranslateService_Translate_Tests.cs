using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.MarkdownTranslator;
using Mostlylucid.Test.TranslationService.Helpers;

namespace Mostlylucid.Test.TranslationService;

public class TranslateService_Translate_Tests
{
    [Fact]
    public async Task Test_Translate_Success()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMarkdownTranslatorServiceCollection();

        var serviceProvider = services.BuildServiceProvider();
        var translateService = serviceProvider.GetRequiredService<IMarkdownTranslatorService>();
        var markdown = "This is a test";
        var targetLang = "es";
        var translated = await translateService.TranslateMarkdown(markdown, targetLang, CancellationToken.None, null);
        Assert.Equal("Esto es una prueba", translated);
    }
    
    [Fact(DisplayName = "Tests what happens when the service returns an error")]
    public async Task Test_Translate_Fail()
    {
        var services = new ServiceCollection();
        services.AddMarkdownTranslatorServiceCollection();

        var serviceProvider = services.BuildServiceProvider();
        var translateService = serviceProvider.GetRequiredService<IMarkdownTranslatorService>();
        var markdown = "This is a test";
        var targetLang = "xx";
        await Assert.ThrowsAsync<TranslateException>(async () => await translateService.TranslateMarkdown(markdown, targetLang, CancellationToken.None, null));
    }

    [Fact(DisplayName = "Tests that text with spaces and formatting is preserved")]
    public async Task Test_Translate_TextWithSpaces_PreservesFormatting()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMarkdownTranslatorServiceCollection();

        var serviceProvider = services.BuildServiceProvider();
        var translateService = serviceProvider.GetRequiredService<IMarkdownTranslatorService>();

        // This tests that spacing within bold text is preserved during translation
        // Previously, sentence splitting would break "Test. New Text " into multiple pieces
        // and lose trailing spaces
        var markdown = "Some **bold text with spaces** here.";
        var targetLang = "es";

        // Act
        var translated = await translateService.TranslateMarkdown(markdown, targetLang, CancellationToken.None, null);

        // Assert
        // The key thing is that markdown structure should be preserved
        // The mock returns "Esto es una prueba" for any input, so we just verify
        // that the markdown bold markers are still present in the output
        Assert.Contains("**", translated); // Should still have bold markers

        // The mock translation service returns a fixed string, so we verify that
        // the markdown structure is maintained
        Assert.Matches(@"\*\*.*?\*\*", translated); // Should have **text** pattern
    }

}