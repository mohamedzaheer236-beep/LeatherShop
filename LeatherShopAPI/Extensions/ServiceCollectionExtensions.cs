using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.Services;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PostgreSQL database context.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        return services;
    }

    /// <summary>
    /// Registers all application services (Interface → Implementation).
    /// Add new service registrations here as the project grows.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // HttpClient-based services
        services.AddHttpClient<IWhatsAppService, WhatsAppService>();

        // Scoped services (one instance per HTTP request — matches DbContext lifetime)
        services.AddScoped<IChatBotService, ChatBotService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IBroadcastService, BroadcastService>();
        services.AddScoped<IPaymentService, PaymentService>();

        // Broadcast background processing (Channel + hosted service)
        services.AddSingleton<BroadcastChannel>();
        services.AddHostedService<BroadcastBackgroundService>();

        // Future registrations go here:
        // services.AddScoped<IAuthService, AuthService>();
        // services.AddScoped<IImageUploadService, ImageUploadService>();
        // services.AddScoped<INotificationService, NotificationService>();

        return services;
    }

    /// <summary>
    /// Configures CORS policies for the Angular admin panel.
    /// </summary>
    public static IServiceCollection AddCorsPolicies(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAngular", policy =>
            {
                var origins = new List<string> { "http://localhost:4200" };
                
                // Add production frontend URL if configured
                var prodFrontend = Environment.GetEnvironmentVariable("FRONTEND_URL");
                if (!string.IsNullOrEmpty(prodFrontend))
                    origins.Add(prodFrontend);

                policy.WithOrigins(origins.ToArray())
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}
