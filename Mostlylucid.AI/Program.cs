using Mostlylucid.AI.Services;
using Mostlylucid.Blog;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Services.Email;
using Mostlylucid.Services.Umami;
using Mostlylucid.Shared.Config;
using OpenTelemetry.Metrics;
using Serilog.Debugging;

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Error)
        .MinimumLevel.Override("Npgsql", Serilog.Events.LogEventLevel.Error)
        .WriteTo.Console()
        .WriteTo.File("logs/boot-*.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
        .CreateBootstrapLogger();

    var builder = WebApplication.CreateBuilder(args);

    var config = builder.Configuration;
    if (builder.Environment.IsDevelopment())
    {
        config.AddUserSecrets<Program>();
    }
    config.AddEnvironmentVariables();

    builder.Host.UseSerilog((context, configuration) =>
    {
        configuration.ReadFrom.Configuration(context.Configuration);
    });

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(8080); // HTTP endpoint
    });

    using var listener = new ActivityListenerConfiguration()
        .Instrument.HttpClientRequests().Instrument
        .AspNetCoreRequests()
        .TraceToSharedLogger();

    builder.Configure<AnalyticsSettings>();
    builder.Configure<SmtpSettings>();

    var services = builder.Services;

    services.AddOpenTelemetry()
        .WithMetrics(metricsBuilder =>
        {
            metricsBuilder.AddAspNetCoreInstrumentation();
            metricsBuilder.AddRuntimeInstrumentation();
            metricsBuilder.AddHttpClientInstrumentation();
            metricsBuilder.AddPrometheusExporter();
        });

    services.AddOutputCache(options =>
    {
        options.AddBasePolicy(policyBuilder => policyBuilder
            .Expire(TimeSpan.FromMinutes(5))
            .With(ctx => !ctx.HttpContext.Request.Headers.ContainsKey("HX-Request")));
    });

    services.AddResponseCaching();
    services.AddHealthChecks();

    // Register services from main Mostlylucid project
    services.SetupUmamiClient(config);
    services.SetupBlog(config, builder.Environment);
    services.SetupEmail(config);

    // AI Article Service
    services.AddScoped<IAIArticleService, AIArticleService>();
    services.AddScoped<AIBaseControllerService>();

    services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });

    services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
        options.SuppressXFrameOptionsHeader = false;
    });

    services.AddCors(options =>
    {
        options.AddPolicy("AllowMostlylucidAI",
            corsBuilder =>
            {
                corsBuilder.WithOrigins("https://www.mostlylucid.ai")
                    .WithOrigins("https://mostlylucid.ai")
                    .WithOrigins("https://localhost:8080")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
    });

    services.AddMvc();

    var app = builder.Build();

    app.UseResponseCompression();
    app.UseSerilogRequestLogging();
    app.UseHealthChecks("/healthz");
    app.MapPrometheusScrapingEndpoint();

    // Migrate database
    using (var scope = app.Services.CreateScope())
    {
        var blogContext = scope.ServiceProvider.GetRequiredService<IMostlylucidDBContext>();
        await blogContext.Database.MigrateAsync();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error/500");
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseOutputCache();
    app.UseResponseCaching();

    var cacheMaxAgeOneWeek = (60 * 60 * 24 * 7).ToString();

    app.UseStaticFiles(new StaticFileOptions
    {
        ServeUnknownFileTypes = true,
        DefaultContentType = "application/octet-stream",
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append(
                "Cache-Control", $"public, max-age={cacheMaxAgeOneWeek}");
        }
    });

    app.UseStatusCodePagesWithReExecute("/error/{0}");

    app.UseRouting();
    app.UseCors("AllowMostlylucidAI");

    app.MapControllerRoute(
        "default",
        "{controller=Home}/{action=Index}/{id?}");

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

// Explicit Program class for user secrets
public partial class Program { }
