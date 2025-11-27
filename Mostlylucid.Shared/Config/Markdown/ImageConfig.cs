namespace Mostlylucid.Shared.Config.Markdown;

/// <summary>
/// Configuration for image processing in markdown
/// </summary>
public class ImageConfig : IConfigSection
{
    public static string Section => "ImageProcessing";

    /// <summary>
    /// Default format for images (can be overridden by querystring)
    /// </summary>
    public string DefaultFormat { get; set; } = "webp";

    /// <summary>
    /// Default quality for images (can be overridden by querystring)
    /// </summary>
    public int DefaultQuality { get; set; } = 50;

    /// <summary>
    /// Primary folder for images (relative to wwwroot)
    /// </summary>
    public string PrimaryImageFolder { get; set; } = "articleimages";

    /// <summary>
    /// Fallback folders to check if image not found in primary folder
    /// Relative to application root
    /// </summary>
    public List<string> FallbackFolders { get; set; } = new()
    {
        "Markdown",
        "Markdown/translated"
    };
}
