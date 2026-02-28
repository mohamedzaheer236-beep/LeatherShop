using System.Text;
using LeatherShopAPI.Data;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Hubs;
using LeatherShopAPI.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Load optional local secrets file (gitignored — never committed to source control)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

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

// Swagger — only exposed in Development for security
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(); // Serve uploaded images from wwwroot

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
