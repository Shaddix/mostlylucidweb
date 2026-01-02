using Mostlylucid.VoiceForm;
using Mostlylucid.VoiceForm.Components;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/voiceform-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting VoiceForm application");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Add Blazor Server services
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // Add VoiceForm services
    builder.Services.SetupVoiceForm(builder.Configuration);

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // API endpoints for audio upload
    app.MapVoiceFormEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
