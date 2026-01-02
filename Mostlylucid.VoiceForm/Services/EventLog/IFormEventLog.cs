using Mostlylucid.VoiceForm.Models.Events;
using Mostlylucid.VoiceForm.Models.State;

namespace Mostlylucid.VoiceForm.Services.EventLog;

/// <summary>
/// Abstraction for form event logging.
/// All events are immutable and logged for audit/replay.
/// This enables deterministic session replay.
/// </summary>
public interface IFormEventLog
{
    /// <summary>
    /// Log a form event
    /// </summary>
    Task LogAsync(FormEvent formEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all events for a session in order
    /// </summary>
    Task<IReadOnlyList<FormEvent>> GetSessionEventsAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replay a session from its events (for debugging/auditing)
    /// </summary>
    Task<FormSession?> ReplaySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}
