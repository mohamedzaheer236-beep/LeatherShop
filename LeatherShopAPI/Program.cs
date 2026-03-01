using System.Text;
using System.Threading.RateLimiting;
using LeatherShopAPI.Data;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Hubs;
using LeatherShopAPI.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;

// QuestPDF Community License (free for revenue < $1M)
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Load optional local secrets file (gitignored — never committed to source control)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// --- DataProtection: use ephemeral keys in containers (we use JWT, not cookies) ---
// Prevents the "keys may be lost when container is destroyed" startup warning.
// This is safe because we don't use cookie auth, antiforgery tokens, or session state.
builder.Services.AddDataProtection()
    .SetApplicationName("LeatherShopAPI")
    .UseEphemeralDataProtectionProvider();

// --- All service registrations in Extensions/ServiceCollectionExtensions.cs ---
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddCorsPolicies();
builder.Services.AddSignalR();

// --- JWT Authentication ---
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException(
        "Jwt:Key is not configured. " +
        "Set it in appsettings.Local.json, dotnet user-secrets, or via Jwt__Key environment variable. " +
        "Must be at least 32 characters.");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };

    // Allow SignalR to receive the JWT from the query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Rate Limiting (per-IP) ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global fixed-window: max 100 requests per minute per IP
    options.AddPolicy("fixed", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Strict policy for auth endpoints: max 10 attempts per minute per IP
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// Railway provides PORT env variable
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

var app = builder.Build();

// --- Auto-migrate database + seed admin user on startup ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Seed default admin if no admin users exist
    if (!db.AdminUsers.Any())
    {
        var adminPassword = app.Configuration["Admin:SeedPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException(
                "Admin:SeedPassword is not configured but no admin users exist in the database. " +
                "Set it in appsettings.Local.json or via Admin__SeedPassword environment variable.");

        db.AdminUsers.Add(new LeatherShopAPI.Models.AdminUser
        {
            Username = "Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        Console.WriteLine("\u2705 Default admin user seeded.");
    }
}

// --- Global exception handling (must be first in pipeline) ---
app.UseMiddleware<ExceptionHandlingMiddleware>();

// --- HTTPS forwarding headers (Railway terminates TLS at the proxy) ---
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// --- HSTS: tell browsers to always use HTTPS (skip in Development) ---
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Swagger — only exposed in Development for security
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(); // Serve uploaded images from wwwroot

app.UseCors("AllowAngular");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
