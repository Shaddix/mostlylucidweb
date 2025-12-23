using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Mostlylucid.SegmentCommerce.ClientFingerprint;
using Mostlylucid.SegmentCommerce.Services;
using Mostlylucid.SegmentCommerce.Services.Attributes;
using Mostlylucid.SegmentCommerce.Services.Embeddings;
using Mostlylucid.SegmentCommerce.Services.Profiles;
using Mostlylucid.SegmentCommerce.Services.Queue;
using Mostlylucid.SegmentCommerce.Services.Segments;
using System.Text;

namespace Mostlylucid.SegmentCommerce.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationAndAuthorization(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var signingKey = configuration["Jwt:SigningKey"];
        var audience = configuration["Jwt:Audience"];
        var authority = configuration["Jwt:Authority"];

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = true;

                if (!string.IsNullOrEmpty(signingKey))
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = !string.IsNullOrEmpty(authority),
                        ValidateAudience = !string.IsNullOrEmpty(audience),
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
                    };
                }
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("admin");
            });
        });

        return services;
    }

    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ProductService>();
        services.AddScoped<InteractionService>();
        services.AddSingleton<GadgetAttributeProvider>();
        
        // Profile services (Zero PII)
        services.AddScoped<IProfileResolver, ProfileResolver>();
        services.AddScoped<ISessionCollector, SessionCollector>();
        
        // Session helpers
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IInterestTrackingService, InterestTrackingService>();
        services.AddScoped<ICartService, CartService>();
        services.AddHttpContextAccessor();
        
        // Segment services
        services.AddSingleton<ISegmentService, SegmentService>();
        services.AddScoped<ISegmentVisualizationService, SegmentVisualizationService>();
        services.AddScoped<IDemoUserService, DemoUserService>();
        
        return services;
    }

    public static IServiceCollection AddClientFingerprint(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ClientFingerprintConfig>(
            configuration.GetSection(ClientFingerprintConfig.SectionName));
        
        services.AddSingleton<IClientFingerprintService, ClientFingerprintService>();
        
        return services;
    }

    public static IServiceCollection RegisterQueueServices(this IServiceCollection services)
    {
        services.AddScoped<IJobQueue, PostgresJobQueue>();
        services.AddScoped<IOutbox, PostgresOutbox>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        
        return services;
    }
}
