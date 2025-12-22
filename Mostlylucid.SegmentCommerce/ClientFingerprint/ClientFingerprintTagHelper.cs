using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace Mostlylucid.SegmentCommerce.ClientFingerprint;

/// <summary>
/// Tag helper that injects the client fingerprint script.
/// Usage: &lt;ml-fingerprint /&gt;
/// </summary>
[HtmlTargetElement("ml-fingerprint")]
public class ClientFingerprintTagHelper : TagHelper
{
    private readonly ClientFingerprintConfig _config;
    private readonly IWebHostEnvironment _env;
    private static string? _cachedScript;
    private static readonly object CacheLock = new();

    public ClientFingerprintTagHelper(
        IOptions<ClientFingerprintConfig> config,
        IWebHostEnvironment env)
    {
        _config = config.Value;
        _env = env;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!_config.Enabled)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "script";
        output.TagMode = TagMode.StartTagAndEndTag;

        var script = GetConfiguredScript();
        output.Content.SetHtmlContent(script);
    }

    private string GetConfiguredScript()
    {
        var baseScript = GetBaseScript();

        return baseScript
            .Replace("%%VERSION%%", "1.0.0")
            .Replace("%%ENDPOINT%%", _config.Endpoint)
            .Replace("%%COLLECT_WEBGL%%", _config.CollectWebGL ? "true" : "false")
            .Replace("%%COLLECT_CANVAS%%", _config.CollectCanvas ? "true" : "false")
            .Replace("%%COLLECT_AUDIO%%", _config.CollectAudio ? "true" : "false")
            .Replace("%%TIMEOUT%%", _config.Timeout.ToString());
    }

    private string GetBaseScript()
    {
        if (_env.IsDevelopment() || _cachedScript == null)
        {
            lock (CacheLock)
            {
                if (_env.IsDevelopment() || _cachedScript == null)
                {
                    var scriptPath = Path.Combine(_env.ContentRootPath, "ClientFingerprint", "fingerprint.js");
                    _cachedScript = File.Exists(scriptPath) 
                        ? File.ReadAllText(scriptPath) 
                        : GetFallbackScript();
                }
            }
        }
        return _cachedScript!;
    }

    private static string GetFallbackScript()
    {
        // Minimal fallback if main script missing
        return """
            (function(){
                var s=[navigator.platform,screen.width+'x'+screen.height,
                       Intl.DateTimeFormat().resolvedOptions().timeZone].join('|');
                var h=5381;for(var i=0;i<s.length;i++)h=((h<<5)+h)+s.charCodeAt(i);
                navigator.sendBeacon&&navigator.sendBeacon('%%ENDPOINT%%',
                    JSON.stringify({h:(h>>>0).toString(16),ts:Date.now()}));
            })();
            """;
    }
}
