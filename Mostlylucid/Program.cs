using System.Security.Cryptography.X509Certificates;
using Mostlylucid.Blog.WatcherService;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Middleware;
using Mostlylucid.Services;
using Mostlylucid.Services.Images;
using OpenTelemetry.Metrics;
using Scalar.AspNetCore;
using Serilog.Debugging;
using Mostlylucid.EmailSubscription;
using Mostlylucid.Services.Email;
using Mostlylucid.Services.Umami;
using Mostlylucid.Shared.Config;
using Mostlylucid.SemanticSearch.Config;
using Mostlylucid.SemanticSearch.Extensions;
using Mostlylucid.SemanticSearch.Services;

try
{  Log.Logger = new LoggerConfiguration()
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
#if DEBUG
        configuration.MinimumLevel.Debug();
        configuration.WriteTo.Console();
        SelfLog.Enable(Console.Error);
        Console.WriteLine($"Serilog Minimum Level: {configuration.MinimumLevel}");
#endif
    });
    var certExists = File.Exists("mostlylucid.pfx");
    var certPassword = config["CertPassword"];
 
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(8080); // HTTP endpoint

        // HTTPS endpoint using SSL certificate
        options.ListenAnyIP(7240, listenOptions =>
        {
            if (certExists)
            {
                var certificate = new X509Certificate2("mostlylucid.pfx", certPassword);
                listenOptions.UseHttps(options => options.ServerCertificate = certificate);
            }
            else
            //Local development without certificate.
                listenOptions.UseHttps();
            
        });
    });;

    using var listener = new ActivityListenerConfiguration()
        .Instrument.HttpClientRequests().Instrument
        .AspNetCoreRequests()
        .TraceToSharedLogger();
    
    builder.Configure<AnalyticsSettings>();
    var auth = builder.Configure<AuthSettings>();
    var translateServiceConfig = builder.Configure<TranslateServiceConfig>();
    builder.Configure<Mostlylucid.Shared.Config.Markdown.ImageConfig>();
    var services = builder.Services;
    services.AddOpenTelemetry()
        .WithMetrics(builder =>
        {
     
            builder.AddAspNetCoreInstrumentation();
            builder.AddRuntimeInstrumentation();
            builder.AddHttpClientInstrumentation();
            builder.AddProcessInstrumentation();
            builder.AddPrometheusExporter();
        });

// Add services to
// the container.
    services.AddOutputCache();
    services.AddResponseCaching();
    services.AddOpenApi();
    services.SetupTranslateService();
    services.SetupOpenSearch(config);
    services.AddHealthChecks();
    services.SetupUmamiData(config);
    services.AddScoped<IUmamiDataSortService, UmamiDataSortService>();
    services.AddScoped<IUmamiUserInfoService, UmamiUserInfoService>();
    services.AddSingleton<IPopularPostsService, PopularPostsService>();
    services.AddScoped<BaseControllerService>();

    // External image download service
    services.AddScoped<ExternalImageDownloadService>();
    services.AddHostedService<ImageDownloadBackgroundService>();

    // Popular posts polling service
    services.AddHostedService<PopularPostsPollingService>();

    services.AddImageSharp().Configure<PhysicalFileSystemCacheOptions>(options => options.CacheFolder = "cache");
    services.SetupEmail(config);
    services.SetupRSS();
    services.SetupBlog(config, builder.Environment);
    services.SetupUmamiClient(config);
    services.AddSemanticSearch(config);
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });

    services.ConfigureEmailProcessor(config);
    services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
        options.SuppressXFrameOptionsHeader = false;
    });
// Setup CORS for Google Auth Use.
    services.AddCors(options =>
    {
        options.AddPolicy("AllowMostlylucid",
            builder =>
            {
                builder.WithOrigins("https://www.mostlylucid.net")
                    .WithOrigins("https://mostlylucid.net")
                    .WithOrigins("https://localhost:7240")
                    .WithOrigins("https://local.mostlylucid.net")
                    .WithOrigins("https://direct.mostlylucid.net")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
    });

// Setup Authentication Options
    services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddGoogle(options =>
        {
            options.ClientId = auth.GoogleClientId;
            options.ClientSecret = auth.GoogleClientSecret;
        });
    services.AddMvc();
    services.AddProgressiveWebApp(new PwaOptions
    {
        RegisterServiceWorker = true,
        RegisterWebmanifest = false, // (Manually register in Layout file)
        Strategy = ServiceWorkerStrategy.NetworkFirst,
        OfflineRoute = "Offline.html"
    });
    var app = builder.Build();

    // Configure Markdig FetchMarkdownExtension with service provider
    Mostlylucid.Markdig.FetchExtension.FetchMarkdownExtension.ConfigureServiceProvider(app.Services);

    app.UseResponseCompression();
    app.UseContentSecurityPolicy();
    app.UseSerilogRequestLogging();
    app.UseHealthChecks("/healthz");
    app.MapPrometheusScrapingEndpoint();
    using (var scope = app.Services.CreateScope())
    {
        var blogContext = scope.ServiceProvider.GetRequiredService<IMostlylucidDBContext>();
        await blogContext.Database.MigrateAsync();
    }

//await app.SetupOpenSearchIndex();
// Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        //Hnadle unhandled exceptions 500 erros
        app.UseExceptionHandler("/error/500");
        //Handle 404 erros
    }
    else
    {
        app.UseDeveloperExceptionPage();
        app.UseHttpsRedirection();
        app.UseHsts();
    }

    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value;
        if (path.EndsWith("RSS", StringComparison.OrdinalIgnoreCase))
        {
            var rss = context.RequestServices.GetRequiredService<UmamiBackgroundSender>();
            await rss.Track("RSS", useDefaultUserAgent: true);
        }

        await next();
    });
    app.UseOutputCache();
    app.UseResponseCaching();
    app.UseImageSharp();

    var cacheMaxAgeOneWeek = (60 * 60 * 24 * 7).ToString();


    app.UseStaticFiles(new StaticFileOptions
    {
        ServeUnknownFileTypes = true, // This is necessary if serving uncommon file types
        DefaultContentType = "application/octet-stream", // Fallback for unknown types
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append(
                "Cache-Control", $"public, max-age={cacheMaxAgeOneWeek}");
            var fileExt = Path.GetExtension(ctx.File.Name);
            if (fileExt.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.ContentType = "application/pdf";
            }
            else if (fileExt.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            }
        }
    });

    // Add slug redirect middleware before status code pages
    // This allows automatic redirects for learned slug mappings
    app.UseSlugRedirect();

    app.UseStatusCodePagesWithReExecute("/error/{0}");

    app.UseRouting();
    app.UseCors("AllowMostlylucid");
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    await app.PopulateBlog();

    // Initialize semantic search (if enabled and Qdrant is available)
    try
    {
        using var scope = app.Services.CreateScope();
        var semanticConfig = scope.ServiceProvider.GetRequiredService<SemanticSearchConfig>();
        if (semanticConfig.Enabled)
        {
            var semanticSearch = scope.ServiceProvider.GetRequiredService<ISemanticSearchService>();
            await semanticSearch.InitializeAsync();
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to initialize semantic search - continuing without it");
    }

    app.MapGet("/robots.txt", async httpContext =>
    {
        var robotsContent =
            $"User-agent: *\nDisallow: \nDisallow: /cgi-bin/\nSitemap: https://{httpContext.Request.Host}/sitemap.xml";
        httpContext.Response.ContentType = "text/plain";
        await httpContext.Response.WriteAsync(robotsContent);
    }).CacheOutput(policyBuilder =>
    {
        policyBuilder.Expire(TimeSpan.FromDays(60));
        policyBuilder.Cache();
    });


    app.MapControllerRoute(
        "sitemap",
        "sitemap.xml",
        new { controller = "Sitemap", action = "Index" });
    app.MapControllerRoute(
        "default",
        "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}

catch (Exception ex)
{
    if(args.Contains("migrate"))
    {
        Log.Information("Migration complete");
        return;
    }
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}