using System.Text.Json;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Mostlylucid.SegmentCommerce.Services.Profiles;

namespace Mostlylucid.SegmentCommerce.TagHelpers;

/// <summary>
/// Tag helper that renders session configuration as a script tag with JSON data.
/// Similar to HTMX.NET's antiforgery approach - embeds session token in the page
/// so JavaScript can send it in headers without using cookies or localStorage.
/// 
/// Usage: <sc-session></sc-session>
/// 
/// This enables truly cookieless "Session Only" mode where:
/// - Server generates session token per page render
/// - Token embedded in page via script tag (avoids Htmx.TagHelpers meta tag interference)
/// - JavaScript reads token and sends in X-Session-ID header
/// - No cookies, no localStorage needed
/// </summary>
[HtmlTargetElement("sc-session")]
public class SessionConfigTagHelper : TagHelper
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProfileResolver _profileResolver;
    
    public SessionConfigTagHelper(
        IHttpContextAccessor httpContextAccessor,
        IProfileResolver profileResolver)
    {
        _httpContextAccessor = httpContextAccessor;
        _profileResolver = profileResolver;
    }
    
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            output.SuppressOutput();
            return;
        }
        
        // Get or create session
        var session = await _profileResolver.GetOrCreateSessionAsync(httpContext);
        
        // Build config object
        var config = new SessionConfig
        {
            SessionId = session.SessionKey,
            Mode = session.IdentificationMode.ToString().ToLowerInvariant(),
            ProfileId = session.PersistentProfileId?.ToString(),
            HeaderName = "X-Session-ID"
        };
        
        // Serialize to JSON
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        
        // Output as a JSON script tag to avoid Htmx.TagHelpers meta tag interference
        output.TagName = "script";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "application/json");
        output.Attributes.SetAttribute("id", "sc-session");
        output.Content.SetHtmlContent(json);
    }
    
    private class SessionConfig
    {
        public string SessionId { get; set; } = "";
        public string Mode { get; set; } = "none";
        public string? ProfileId { get; set; }
        public string HeaderName { get; set; } = "X-Session-ID";
    }
}
