using System.Collections.Concurrent;

namespace Mostlylucid.MarkdownTranslator;

/// <summary>
/// Tracks translation failures per language and temporarily skips problematic languages
/// </summary>
public class LanguageFailureTracker
{
    private readonly ConcurrentDictionary<string, LanguageFailureInfo> _failures = new();
    private readonly int _maxConsecutiveFailures;
    private readonly TimeSpan _resetInterval;
    private DateTime _lastReset = DateTime.UtcNow;

    public LanguageFailureTracker(int maxConsecutiveFailures = 3, TimeSpan? resetInterval = null)
    {
        _maxConsecutiveFailures = maxConsecutiveFailures;
        _resetInterval = resetInterval ?? TimeSpan.FromHours(24); // Reset daily by default
    }

    /// <summary>
    /// Check if we should reset the failure tracking (daily reset)
    /// </summary>
    private void CheckReset()
    {
        if (DateTime.UtcNow - _lastReset > _resetInterval)
        {
            _failures.Clear();
            _lastReset = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Record a successful translation for a language
    /// </summary>
    public void RecordSuccess(string language)
    {
        CheckReset();
        _failures.TryRemove(language, out _);
    }

    /// <summary>
    /// Record a failed translation for a language
    /// </summary>
    public void RecordFailure(string language)
    {
        CheckReset();

        _failures.AddOrUpdate(
            language,
            new LanguageFailureInfo { ConsecutiveFailures = 1, LastFailure = DateTime.UtcNow },
            (_, info) =>
            {
                info.ConsecutiveFailures++;
                info.LastFailure = DateTime.UtcNow;
                return info;
            });
    }

    /// <summary>
    /// Check if a language should be skipped due to consecutive failures
    /// </summary>
    public bool ShouldSkip(string language)
    {
        CheckReset();

        if (_failures.TryGetValue(language, out var info))
        {
            return info.ConsecutiveFailures >= _maxConsecutiveFailures;
        }

        return false;
    }

    /// <summary>
    /// Get failure count for a language
    /// </summary>
    public int GetFailureCount(string language)
    {
        CheckReset();

        if (_failures.TryGetValue(language, out var info))
        {
            return info.ConsecutiveFailures;
        }

        return 0;
    }

    /// <summary>
    /// Get all currently skipped languages
    /// </summary>
    public IEnumerable<string> GetSkippedLanguages()
    {
        CheckReset();

        return _failures
            .Where(kvp => kvp.Value.ConsecutiveFailures >= _maxConsecutiveFailures)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private class LanguageFailureInfo
    {
        public int ConsecutiveFailures { get; set; }
        public DateTime LastFailure { get; set; }
    }
}
