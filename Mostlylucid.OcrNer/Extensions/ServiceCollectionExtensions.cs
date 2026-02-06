using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.OcrNer.Config;
using Mostlylucid.OcrNer.Services;
using Mostlylucid.OcrNer.Services.Preprocessing;

namespace Mostlylucid.OcrNer.Extensions;

/// <summary>
/// Extension methods for registering Mostlylucid.OcrNer services.
///
/// Usage in Program.cs:
/// <code>
/// // Option 1: From appsettings.json
/// builder.Services.AddOcrNer(builder.Configuration);
///
/// // Option 2: Inline configuration
/// builder.Services.AddOcrNer(config =>
/// {
///     config.EnableOcr = true;
///     config.MinConfidence = 0.6f;
///     config.TesseractLanguage = "eng";
/// });
/// </code>
///
/// This registers:
/// - INerService → NerService (singleton) - BERT NER from text
/// - IOcrService → OcrService (singleton) - Tesseract OCR from images
/// - IOcrNerPipeline → OcrNerPipeline (singleton) - Combined OCR + NER
/// - IVisionService → VisionService (singleton) - Florence-2 vision captioning
/// - ModelDownloader (singleton) - Auto-downloads models on first use
/// - ImagePreprocessor (singleton) - Image preprocessing for OCR
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add OCR, NER, and Vision services using configuration from appsettings.json.
    ///
    /// Expected configuration section: "OcrNer"
    /// <code>
    /// {
    ///   "OcrNer": {
    ///     "EnableOcr": true,
    ///     "TesseractLanguage": "eng",
    ///     "MinConfidence": 0.5,
    ///     "MaxSequenceLength": 512
    ///   }
    /// }
    /// </code>
    /// </summary>
    public static IServiceCollection AddOcrNer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OcrNerConfig>(
            configuration.GetSection("OcrNer"));

        RegisterServices(services);

        return services;
    }

    /// <summary>
    /// Add OCR, NER, and Vision services with inline configuration.
    /// </summary>
    public static IServiceCollection AddOcrNer(
        this IServiceCollection services,
        Action<OcrNerConfig> configure)
    {
        services.Configure(configure);

        RegisterServices(services);

        return services;
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // HttpClient for model downloads
        services.AddHttpClient("OcrNerModelDownloader", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Mostlylucid.OcrNer/1.0");
        });

        // Infrastructure
        services.AddSingleton<ModelDownloader>();
        services.AddSingleton<ImagePreprocessor>();

        // Core services
        services.AddSingleton<INerService, NerService>();
        services.AddSingleton<IOcrService, OcrService>();
        services.AddSingleton<IOcrNerPipeline, OcrNerPipeline>();

        // Florence-2 vision service
        services.AddSingleton<IVisionService, VisionService>();
    }
}
