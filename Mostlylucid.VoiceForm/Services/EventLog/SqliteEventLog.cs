using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Mostlylucid.VoiceForm.Config;
using Mostlylucid.VoiceForm.Models.Events;
using Mostlylucid.VoiceForm.Models.State;

namespace Mostlylucid.VoiceForm.Services.EventLog;

/// <summary>
/// SQLite-based event log for form sessions.
/// Provides durable, ordered event storage for audit and replay.
/// </summary>
public class SqliteEventLog : IFormEventLog, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteEventLog> _logger;
    private bool _initialized;

    public SqliteEventLog(VoiceFormConfig config, ILogger<SqliteEventLog> logger)
    {
        _logger = logger;

        // Ensure data directory exists
        var dbPath = config.EventLogDbPath;
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={dbPath}";
    }

    public async Task LogAsync(FormEvent formEvent, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        _logger.LogDebug("Logging event {EventType} for session {SessionId}",
            formEvent.EventType, formEvent.SessionId);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Ensure session exists
        await EnsureSessionExistsAsync(connection, formEvent, cancellationToken);

        // Insert event
        const string sql = """
            INSERT INTO events (session_id, sequence_number, event_type, field_id, timestamp, payload)
            VALUES (@sessionId, @seq, @type, @fieldId, @timestamp, @payload)
            """;

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@sessionId", formEvent.SessionId);
        cmd.Parameters.AddWithValue("@seq", formEvent.SequenceNumber);
        cmd.Parameters.AddWithValue("@type", formEvent.EventType);
        cmd.Parameters.AddWithValue("@fieldId", (object?)formEvent.FieldId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@timestamp", formEvent.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@payload", formEvent.ToPayloadJson());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FormEvent>> GetSessionEventsAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var events = new List<FormEvent>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT sequence_number, event_type, field_id, timestamp, payload
            FROM events
            WHERE session_id = @sessionId
            ORDER BY sequence_number
            """;

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@sessionId", sessionId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var seq = reader.GetInt32(0);
            var eventType = reader.GetString(1);
            var fieldId = reader.IsDBNull(2) ? null : reader.GetString(2);
            var timestamp = DateTime.Parse(reader.GetString(3));
            var payload = reader.GetString(4);

            var evt = DeserializeEvent(sessionId, seq, eventType, fieldId, timestamp, payload);
            if (evt != null)
            {
                events.Add(evt);
            }
        }

        return events;
    }

    public async Task<FormSession?> ReplaySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        // For now, just return null - full replay would require form schema
        // This is a placeholder for future implementation
        _logger.LogWarning("Session replay not yet implemented");
        return null;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string createTables = """
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                form_id TEXT NOT NULL,
                form_version INTEGER NOT NULL,
                started_at TEXT NOT NULL,
                completed_at TEXT,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                sequence_number INTEGER NOT NULL,
                event_type TEXT NOT NULL,
                field_id TEXT,
                timestamp TEXT NOT NULL,
                payload TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(id),
                UNIQUE (session_id, sequence_number)
            );

            CREATE INDEX IF NOT EXISTS idx_events_session ON events(session_id, sequence_number);
            """;

        await using var cmd = new SqliteCommand(createTables, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        _initialized = true;
        _logger.LogInformation("SQLite event log initialized");
    }

    private async Task EnsureSessionExistsAsync(
        SqliteConnection connection,
        FormEvent formEvent,
        CancellationToken cancellationToken)
    {
        // Check if session exists
        const string checkSql = "SELECT 1 FROM sessions WHERE id = @id";
        await using var checkCmd = new SqliteCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue("@id", formEvent.SessionId);

        var exists = await checkCmd.ExecuteScalarAsync(cancellationToken) != null;
        if (exists) return;

        // Create session if this is a SessionStarted event
        if (formEvent is SessionStartedEvent startEvent)
        {
            const string insertSql = """
                INSERT INTO sessions (id, form_id, form_version, started_at, status)
                VALUES (@id, @formId, @version, @startedAt, @status)
                """;

            await using var insertCmd = new SqliteCommand(insertSql, connection);
            insertCmd.Parameters.AddWithValue("@id", formEvent.SessionId);
            insertCmd.Parameters.AddWithValue("@formId", startEvent.FormId);
            insertCmd.Parameters.AddWithValue("@version", startEvent.FormVersion);
            insertCmd.Parameters.AddWithValue("@startedAt", formEvent.Timestamp.ToString("O"));
            insertCmd.Parameters.AddWithValue("@status", FormStatus.InProgress.ToString());

            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private FormEvent? DeserializeEvent(
        string sessionId,
        int seq,
        string eventType,
        string? fieldId,
        DateTime timestamp,
        string payload)
    {
        try
        {
            var payloadObj = JsonSerializer.Deserialize<JsonElement>(payload);

            return eventType switch
            {
                "SessionStarted" => new SessionStartedEvent
                {
                    SessionId = sessionId,
                    SequenceNumber = seq,
                    Timestamp = timestamp,
                    FormId = payloadObj.GetProperty("FormId").GetString()!,
                    FormVersion = payloadObj.GetProperty("FormVersion").GetInt32()
                },
                "TranscriptReceived" => new TranscriptReceivedEvent
                {
                    SessionId = sessionId,
                    SequenceNumber = seq,
                    Timestamp = timestamp,
                    FieldId = fieldId,
                    Transcript = payloadObj.GetProperty("Transcript").GetString()!,
                    Confidence = payloadObj.GetProperty("Confidence").GetDouble(),
                    DurationMs = payloadObj.GetProperty("DurationMs").GetInt32()
                },
                "ExtractionAttempt" => new ExtractionAttemptEvent
                {
                    SessionId = sessionId,
                    SequenceNumber = seq,
                    Timestamp = timestamp,
                    FieldId = fieldId,
                    ExtractedValue = payloadObj.TryGetProperty("ExtractedValue", out var ev) ? ev.GetString() : null,
                    Confidence = payloadObj.GetProperty("Confidence").GetDouble(),
                    NeedsConfirmation = payloadObj.GetProperty("NeedsConfirmation").GetBoolean(),
                    Reason = payloadObj.TryGetProperty("Reason", out var r) ? r.GetString() : null
                },
                "FieldConfirmed" => new FieldConfirmedEvent
                {
                    SessionId = sessionId,
                    SequenceNumber = seq,
                    Timestamp = timestamp,
                    FieldId = fieldId,
                    Value = payloadObj.GetProperty("Value").GetString()!,
                    ConfirmedBy = payloadObj.GetProperty("ConfirmedBy").GetString()!
                },
                "FieldRejected" => new FieldRejectedEvent
                {
                    SessionId = sessionId,
                    SequenceNumber = seq,
                    Timestamp = timestamp,
                    FieldId = fieldId,
                    AttemptedValue = payloadObj.GetProperty("AttemptedValue").GetString()!,
                    Reason = payloadObj.GetProperty("Reason").GetString()!
                },
                "FieldSkipped" => new FieldSkippedEvent
                {
                    SessionId = sessionId,
                    SequenceNumber = seq,
                    Timestamp = timestamp,
                    FieldId = fieldId
                },
                "FormCompleted" => new FormCompletedEvent
                {
                    SessionId = sessionId,
                    SequenceNumber = seq,
                    Timestamp = timestamp,
                    FinalValues = JsonSerializer.Deserialize<Dictionary<string, string?>>(
                        payloadObj.GetProperty("FinalValues").GetRawText())!
                },
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize event type {EventType}", eventType);
            return null;
        }
    }

    public void Dispose()
    {
        // SqliteConnection is disposed after each operation
    }
}
