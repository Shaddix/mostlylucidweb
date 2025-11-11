/*
 * ⚠️ SECURITY WARNING ⚠️
 *
 * THIS IS A DEMONSTRATION PROJECT FOR EDUCATIONAL PURPOSES ONLY
 *
 * This demo illustrates the CONCEPTS behind a hidden secure chat system,
 * but it is NOT suitable for production use and has MANY security weaknesses:
 *
 * - Hardcoded authentication credentials
 * - No encryption at rest
 * - No rate limiting
 * - No session timeouts
 * - No audit logging
 * - Minimal input validation
 * - No CSRF protection
 * - No proper secret management
 * - No anti-tampering measures
 * - No defense against traffic analysis
 *
 * DO NOT USE THIS CODE TO PROTECT REAL PEOPLE IN DANGEROUS SITUATIONS
 *
 * For production deployment of similar systems:
 * - Engage professional security consultants
 * - Conduct thorough threat modeling
 * - Implement multiple layers of encryption
 * - Add comprehensive audit logging
 * - Perform penetration testing
 * - Implement incident response procedures
 * - Ensure legal compliance
 */

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5000", "https://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Demo/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Demo}/{action=Company}/{id?}");

app.MapHub<Mostlylucid.SecureChat.Demo.Hubs.SecureChatHub>("/securechat");

app.Run();
