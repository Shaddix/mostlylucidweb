using Mostlylucid.VoiceForm.Config;
using Mostlylucid.VoiceForm.Services.Confirmation;
using Mostlylucid.VoiceForm.Services.EventLog;
using Mostlylucid.VoiceForm.Services.Extraction;
using Mostlylucid.VoiceForm.Services.Orchestration;
using Mostlylucid.VoiceForm.Services.StateMachine;
using Mostlylucid.VoiceForm.Services.Stt;
using Mostlylucid.VoiceForm.Services.Validation;
using Polly;
using Polly.Extensions.Http;

namespace Mostlylucid.VoiceForm;

public static class Setup
{
    public static void SetupVoiceForm(this IServiceCollection services, IConfiguration config)
    {
        // Configuration
        var voiceFormConfig = services.ConfigurePOCO<VoiceFormConfig>(config.GetSection(VoiceFormConfig.Section));

        // HTTP clients with resilience policies
        services.AddHttpClient<ISttService, WhisperDockerService>(client =>
        {
            client.BaseAddress = new Uri(voiceFormConfig.Whisper.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(voiceFormConfig.Whisper.TimeoutSeconds);
        })
        .AddPolicyHandler(GetRetryPolicy());

        services.AddHttpClient<IFieldExtractor, OllamaFieldExtractor>(client =>
        {
            client.BaseAddress = new Uri(voiceFormConfig.Ollama.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(voiceFormConfig.Ollama.TimeoutSeconds);
        })
        .AddPolicyHandler(GetRetryPolicy());

        // Core services
        services.AddScoped<IFormValidator, FormValidatorService>();
        services.AddScoped<IConfirmationPolicy, ThresholdConfirmationPolicy>();
        services.AddScoped<IFormStateMachine, FormStateMachine>();
        services.AddSingleton<IFormEventLog, SqliteEventLog>();
        services.AddScoped<IFormOrchestrator, FormOrchestrator>();

        // Form schema loader
        services.AddSingleton<IFormSchemaLoader, JsonFormSchemaLoader>();
    }

    public static void MapVoiceFormEndpoints(this WebApplication app)
    {
        app.MapPost("/api/voiceform/audio", async (
            HttpRequest request,
            IFormOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms, ct);
            var audioData = ms.ToArray();

            var result = await orchestrator.ProcessAudioAsync(audioData, ct);
            return Results.Ok(result);
        });

        app.MapPost("/api/voiceform/confirm", async (
            IFormOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.ConfirmValueAsync(ct);
            return Results.Ok(result);
        });

        app.MapPost("/api/voiceform/reject", async (
            IFormOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.RejectValueAsync(ct);
            return Results.Ok(result);
        });

        app.MapGet("/api/voiceform/session", (IFormOrchestrator orchestrator) =>
        {
            return Results.Ok(orchestrator.GetCurrentSession());
        });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
