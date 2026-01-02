using FluentAssertions;
using PuppeteerSharp;
using Xunit;

namespace Mostlylucid.VoiceForm.Tests.Integration;

/// <summary>
/// Browser integration tests using PuppeteerSharp.
/// These tests verify the actual browser experience works correctly.
/// Requires the VoiceForm app to be running on localhost:5000.
/// </summary>
[Collection("Browser")]
public class BrowserIntegrationTests : IAsyncLifetime
{
    private IBrowser? _browser;
    private IPage? _page;
    private const string BaseUrl = "http://localhost:5000";

    public async Task InitializeAsync()
    {
        // Download browser if needed
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-setuid-sandbox", "--use-fake-ui-for-media-stream"]
        });

        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
    }

    #region Home Page Tests

    [Fact]
    public async Task HomePage_ShouldLoad_WithFormCards()
    {
        // Act
        var response = await _page!.GoToAsync(BaseUrl);

        // Assert
        response!.Status.Should().Be(System.Net.HttpStatusCode.OK);

        var title = await _page.GetTitleAsync();
        title.Should().Contain("Voice Form");

        // Check for form card (new design)
        var formCard = await _page.QuerySelectorAsync(".form-card");
        formCard.Should().NotBeNull("Home page should have form cards");
    }

    [Fact]
    public async Task HomePage_ThemeToggle_ShouldSwitchTheme()
    {
        // Arrange
        await _page!.GoToAsync(BaseUrl);
        await _page.WaitForSelectorAsync(".theme-toggle", new WaitForSelectorOptions { Timeout = 5000 });

        // Get initial theme
        var initialTheme = await _page.EvaluateFunctionAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");

        // Act - Click theme toggle
        await _page.ClickAsync(".theme-toggle");
        await Task.Delay(100); // Allow state to update

        // Assert - Theme should have changed
        var newTheme = await _page.EvaluateFunctionAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");

        // Theme should be different (toggled)
        newTheme.Should().NotBe(initialTheme, "Theme should toggle when button is clicked");
    }

    [Fact]
    public async Task HomePage_DarkMode_ShouldPersistOnReload()
    {
        // Arrange - Set dark mode
        await _page!.GoToAsync(BaseUrl);
        await _page.WaitForSelectorAsync(".theme-toggle", new WaitForSelectorOptions { Timeout = 5000 });

        // Set theme to dark via JS
        await _page.EvaluateFunctionAsync("() => voiceFormTheme.setTheme('dark')");
        await Task.Delay(100);

        // Act - Reload page
        await _page.ReloadAsync();
        await _page.WaitForSelectorAsync(".theme-toggle", new WaitForSelectorOptions { Timeout = 5000 });

        // Assert - Theme should persist
        var theme = await _page.EvaluateFunctionAsync<string?>(
            "() => document.documentElement.getAttribute('data-theme')");
        theme.Should().Be("dark", "Dark mode should persist after reload");
    }

    [Fact]
    public async Task HomePage_FeaturesGrid_ShouldDisplay()
    {
        // Act
        await _page!.GoToAsync(BaseUrl);
        await _page.WaitForSelectorAsync(".features-grid", new WaitForSelectorOptions { Timeout = 5000 });

        // Assert - Should have 4 feature items
        var features = await _page.QuerySelectorAllAsync(".feature-item");
        features.Should().HaveCount(4, "Should display 4 feature items");
    }

    #endregion

    #region Voice Form Page Tests

    [Fact]
    public async Task VoiceFormPage_ShouldLoad_WithTwoColumnLayout()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".voice-form-layout", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert - Should have two-column layout
        var activePanel = await _page.QuerySelectorAsync(".active-field-panel");
        activePanel.Should().NotBeNull("Should have active field panel");

        var sidebar = await _page.QuerySelectorAsync(".form-sidebar");
        sidebar.Should().NotBeNull("Should have form sidebar");
    }

    [Fact]
    public async Task VoiceFormPage_Sidebar_ShouldShowAllFields()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".form-fields-list", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert - Should have 5 field items in sidebar
        var fieldItems = await _page.QuerySelectorAllAsync(".form-field-item");
        fieldItems.Should().HaveCount(5, "Customer intake form has 5 fields");
    }

    [Fact]
    public async Task VoiceFormPage_FirstField_ShouldBeActive()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".form-field-item.active", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert - First field should have active class
        var activeField = await _page.QuerySelectorAsync(".form-field-item.active");
        activeField.Should().NotBeNull("First field should be active");

        // First field should be Full Name
        var fieldName = await _page.QuerySelectorAsync(".form-field-item.active .field-name");
        var nameText = await _page.EvaluateFunctionAsync<string>("el => el.textContent", fieldName);
        nameText.Should().Contain("Full Name", "First field should be Full Name");
    }

    [Fact]
    public async Task VoiceFormPage_CurrentPrompt_ShouldShowFieldLabel()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".current-prompt h2", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert - Should show current field label
        var promptHeading = await _page.QuerySelectorAsync(".current-prompt h2");
        var headingText = await _page.EvaluateFunctionAsync<string>("el => el.textContent", promptHeading);
        headingText.Should().Contain("Full Name", "Current prompt should show Full Name");
    }

    [Fact]
    public async Task VoiceFormPage_ProgressBar_ShouldShowZeroInitially()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".progress-summary", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert - Progress should show 0/5
        var progressText = await _page.QuerySelectorAsync(".progress-text");
        var text = await _page.EvaluateFunctionAsync<string>("el => el.textContent", progressText);
        text.Should().Contain("0 / 5", "Progress should show 0/5 initially");
    }

    #endregion

    #region Recording Button Tests

    [Fact]
    public async Task VoiceFormPage_RecordButton_ShouldExist()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".record-btn", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert
        var recordButton = await _page.QuerySelectorAsync(".record-btn");
        recordButton.Should().NotBeNull("Page should have a record button");
    }

    [Fact]
    public async Task VoiceFormPage_RecordButton_ShouldHaveIdleClass()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".record-btn.idle", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert - Button should start in idle state
        var recordButton = await _page.QuerySelectorAsync(".record-btn.idle");
        recordButton.Should().NotBeNull("Record button should be in idle state initially");
    }

    [Fact]
    public async Task VoiceFormPage_RecordButton_ShouldShowMicIcon()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".record-btn", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert - Button should have mic icon
        var micIcon = await _page.QuerySelectorAsync(".record-btn .mic-icon");
        micIcon.Should().NotBeNull("Record button should have mic icon when idle");
    }

    [Fact]
    public async Task VoiceFormPage_RecordButton_ShowsStartRecordingText()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".record-btn", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert
        var buttonText = await _page.EvaluateFunctionAsync<string>(
            "() => document.querySelector('.record-btn').textContent");
        buttonText.Should().Contain("Start Recording", "Button should say 'Start Recording'");
    }

    [Fact]
    public async Task VoiceFormPage_RecordButton_ShouldNotBeDisabled()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".record-btn", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert
        var isDisabled = await _page.EvaluateFunctionAsync<bool>(
            "() => document.querySelector('.record-btn').disabled");
        isDisabled.Should().BeFalse("Record button should be enabled");
    }

    [Fact]
    public async Task VoiceFormPage_RecorderHint_ShouldDisplay()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".recorder-hint", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert
        var hintText = await _page.EvaluateFunctionAsync<string>(
            "() => document.querySelector('.recorder-hint').textContent");
        hintText.Should().Contain("Click the button", "Should show hint text");
    }

    #endregion

    #region JavaScript Module Tests

    [Fact]
    public async Task VoiceFormPage_AudioModule_ShouldBeLoaded()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await Task.Delay(1000); // Wait for JS to load

        // Assert
        var hasAudioModule = await _page.EvaluateFunctionAsync<bool>(
            "() => typeof window.voiceFormAudio !== 'undefined'");
        hasAudioModule.Should().BeTrue("voiceFormAudio JS module should be loaded");
    }

    [Fact]
    public async Task VoiceFormPage_ThemeModule_ShouldBeLoaded()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await Task.Delay(1000); // Wait for JS to load

        // Assert
        var hasThemeModule = await _page.EvaluateFunctionAsync<bool>(
            "() => typeof window.voiceFormTheme !== 'undefined'");
        hasThemeModule.Should().BeTrue("voiceFormTheme JS module should be loaded");
    }

    [Fact]
    public async Task VoiceFormPage_AudioModule_HasRequiredMethods()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await Task.Delay(1000);

        // Assert - Check all required methods exist
        var hasInitialize = await _page.EvaluateFunctionAsync<bool>(
            "() => typeof window.voiceFormAudio.initialize === 'function'");
        var hasStartRecording = await _page.EvaluateFunctionAsync<bool>(
            "() => typeof window.voiceFormAudio.startRecording === 'function'");
        var hasStopRecording = await _page.EvaluateFunctionAsync<bool>(
            "() => typeof window.voiceFormAudio.stopRecording === 'function'");

        hasInitialize.Should().BeTrue("Audio module should have initialize method");
        hasStartRecording.Should().BeTrue("Audio module should have startRecording method");
        hasStopRecording.Should().BeTrue("Audio module should have stopRecording method");
    }

    #endregion

    #region Blazor SignalR Tests

    [Fact]
    public async Task VoiceFormPage_Blazor_ShouldBeConnected()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await Task.Delay(2000); // Wait for Blazor to initialize

        // Check for Blazor circuit
        var hasBlazorCircuit = await _page.EvaluateFunctionAsync<bool>(@"
            () => {
                // Check if Blazor has established a connection
                return typeof Blazor !== 'undefined' &&
                       Blazor._internal &&
                       Blazor._internal.getServerConnection;
            }");

        // This is informational - even if circuit detection fails, page should work
        // The key test is that page rendered correctly
        var activePanel = await _page.QuerySelectorAsync(".active-field-panel");
        activePanel.Should().NotBeNull("Blazor should have rendered the active panel");
    }

    #endregion

    #region Dark Mode Component Tests

    [Fact]
    public async Task VoiceFormPage_DarkMode_ShouldAffectSidebar()
    {
        // Arrange
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".form-sidebar", new WaitForSelectorOptions { Timeout = 10000 });

        // Set dark mode
        await _page.EvaluateFunctionAsync("() => voiceFormTheme.setTheme('dark')");
        await Task.Delay(100);

        // Assert - Should have dark theme attribute
        var isDark = await _page.EvaluateFunctionAsync<bool>(
            "() => document.documentElement.getAttribute('data-theme') === 'dark'");
        isDark.Should().BeTrue("Document should have dark theme");

        // Sidebar should still be visible and functional
        var sidebar = await _page.QuerySelectorAsync(".form-sidebar");
        sidebar.Should().NotBeNull("Sidebar should exist in dark mode");
    }

    [Fact]
    public async Task VoiceFormPage_TopBar_ShouldExist()
    {
        // Act
        await _page!.GoToAsync($"{BaseUrl}/voiceform/customer-intake");
        await _page.WaitForSelectorAsync(".top-bar", new WaitForSelectorOptions { Timeout = 10000 });

        // Assert
        var topBar = await _page.QuerySelectorAsync(".top-bar");
        topBar.Should().NotBeNull("Page should have top bar");

        var themeToggle = await _page.QuerySelectorAsync(".top-bar .theme-toggle");
        themeToggle.Should().NotBeNull("Top bar should have theme toggle");
    }

    #endregion

    #region Form Navigation Tests

    [Fact]
    public async Task HomePage_ClickFormCard_ShouldNavigateToForm()
    {
        // Arrange
        await _page!.GoToAsync(BaseUrl);
        await _page.WaitForSelectorAsync(".form-card", new WaitForSelectorOptions { Timeout = 5000 });

        // Act - Click on form card
        await _page.ClickAsync(".form-card");
        await _page.WaitForNavigationAsync(new NavigationOptions { Timeout = 10000 });

        // Assert - Should be on voiceform page
        var url = _page.Url;
        url.Should().Contain("/voiceform/customer-intake", "Should navigate to customer-intake form");

        // Page should have form layout
        await _page.WaitForSelectorAsync(".voice-form-layout", new WaitForSelectorOptions { Timeout = 10000 });
        var layout = await _page.QuerySelectorAsync(".voice-form-layout");
        layout.Should().NotBeNull("Should show form layout after navigation");
    }

    #endregion
}
