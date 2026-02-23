using System.Text;
using LeatherShopAPI.Data;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Auto-generate admin password hash if placeholder ---
var adminSection = builder.Configuration.GetSection("Admin");
var currentHash = adminSection["PasswordHash"] ?? "";
if (currentHash == "$2a$11$placeholder" || string.IsNullOrEmpty(currentHash))
{
    var hash = BCrypt.Net.BCrypt.HashPassword("admin123");
    // Update in-memory config and write to appsettings.json
    var appSettingsPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.json");
    var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(appSettingsPath));
    using var stream = new MemoryStream();
    using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
    {
        writer.WriteStartObject();
        foreach (var prop in json.RootElement.EnumerateObject())
        {
            if (prop.Name == "Admin")
            {
                writer.WriteStartObject("Admin");
                writer.WriteString("Username", adminSection["Username"] ?? "admin");
                writer.WriteString("PasswordHash", hash);
                writer.WriteEndObject();
            }
            else
            {
                prop.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }
    File.WriteAllText(appSettingsPath, Encoding.UTF8.GetString(stream.ToArray()));
    builder.Configuration["Admin:PasswordHash"] = hash;
    Console.WriteLine("✅ Admin password hash generated. Default credentials: admin / admin123");
}

// --- All service registrations in Extensions/ServiceCollectionExtensions.cs ---
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddCorsPolicies();

// --- JWT Authentication ---
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured in appsettings.json");
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
});
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- Auto-migrate database on startup ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// --- Global exception handling (must be first in pipeline) ---
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
