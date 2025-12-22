using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Mostlylucid.SegmentCommerce.ClientFingerprint;

/// <summary>
/// API controller for receiving client fingerprint hashes.
/// 
/// PRIVACY GUARANTEES:
/// - Does NOT log or store IP addresses
/// - Does NOT receive raw fingerprint signals
/// - Only receives a client-computed hash
/// - Re-hashes with server key for session ID
/// </summary>
[ApiController]
[Route("api/fingerprint")]
public class FingerprintController : ControllerBase
{
    private readonly IClientFingerprintService _fingerprintService;
    private readonly ClientFingerprintConfig _config;
    private readonly ILogger<FingerprintController> _logger;

    public FingerprintController(
        IClientFingerprintService fingerprintService,
        IOptions<ClientFingerprintConfig> config,
        ILogger<FingerprintController> logger)
    {
        _fingerprintService = fingerprintService;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Receive fingerprint hash from browser (via sendBeacon or XHR).
    /// Returns 204 No Content for silent success.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    public IActionResult ReceiveFingerprint([FromBody] FingerprintRequest request)
    {
        if (!_config.Enabled)
            return NoContent();

        if (string.IsNullOrEmpty(request.Hash))
            return NoContent();

        try
        {
            // Generate keyed session ID from client hash
            var sessionId = _fingerprintService.GenerateSessionId(request.Hash);
            
            // Store in request context for downstream use
            _fingerprintService.SetSessionId(HttpContext, sessionId);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                // Only log truncated session ID, never the full hash
                _logger.LogDebug("Fingerprint session: {SessionId}", sessionId[..8] + "...");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fingerprint");
            return NoContent();
        }
    }

    /// <summary>
    /// Health check - does not expose any sensitive info.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { enabled = _config.Enabled, v = "1.0" });
    }
}
