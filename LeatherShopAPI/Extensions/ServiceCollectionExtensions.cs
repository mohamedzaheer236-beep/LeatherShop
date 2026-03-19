using Microsoft.EntityFrameworkCore;
using Asp.Versioning;
using LeatherShopAPI.Data;
using LeatherShopAPI.Services;
using LeatherShopAPI.Services.ChatBot;
using LeatherShopAPI.Services.ChatBot.Handlers;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PostgreSQL database context.
    /// Supports both standard connection strings and Railway's DATABASE_URL format.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Railway provides DATABASE_URL in URI format - convert to Npgsql format
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':', 2); // Limit to 2 parts - passwords may contain ':'
            if (userInfo.Length < 2)
                throw new InvalidOperationException(
                    "DATABASE_URL is malformed - expected format: postgres://user:password@host:port/database");
            connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
        }

        // Ensure explicit connection pool settings for predictable behavior under load
        if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("Maximum Pool Size"))
        {
            connectionString += ";Maximum Pool Size=50;Minimum Pool Size=5;Connection Idle Lifetime=60";
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOpts =>
            {
                npgsqlOpts.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                npgsqlOpts.CommandTimeout(30);
            }));

        return services;
    }

    /// <summary>
    /// Configures API versioning with URL segment strategy (e.g., /api/v1/products).
    /// Default version is 1.0; unversioned endpoints (webhook, payment) are not affected.
    /// </summary>
    public static IServiceCollection AddApiVersioningConfig(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

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

        // Named HttpClient for Paytm API calls (Initiate Transaction, Transaction Status)
        services.AddHttpClient("Paytm");

        // In-memory cache for ephemeral chatbot conversation state (pending product/action)
        services.AddMemoryCache();
        services.AddSingleton<ConversationStateService>();

        // Scoped services (one instance per HTTP request - matches DbContext lifetime)
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWebhookProcessingService, WebhookProcessingService>();

        // ChatBot: message sender + domain handlers + orchestrator
        services.AddScoped<BotMessageSender>();
        services.AddScoped<MenuHandler>();
        services.AddScoped<ProductHandler>();
        services.AddScoped<CartHandler>();
        services.AddScoped<CheckoutHandler>();
        services.AddScoped<OrderHistoryHandler>();
        services.AddScoped<IChatBotService, ChatBotService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IBroadcastService, BroadcastService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();
        services.AddScoped<IAdminNotificationService, AdminNotificationService>();

        // Broadcast background processing (Channel + hosted service)
        services.AddSingleton<BroadcastChannel>();
        services.AddHostedService<BroadcastBackgroundService>();

        // Chat cleanup: deletes messages older than 30 days (runs daily)
        services.AddHostedService<ChatCleanupBackgroundService>();

        // WhatsApp outbox processor: delivers critical messages (order confirmations, payment links)
        // with exponential backoff retry. Polls DB every 10s for pending messages.
        services.AddHostedService<WhatsAppOutboxProcessor>();

        // Expired order cleanup: cancels unpaid orders past their PaymentExpiresAt,
        // restores stock quantities, and restores cart items. Runs every 60s.
        services.AddHostedService<ExpiredOrderCleanupService>();

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
                      .AllowAnyMethod()
                      .AllowCredentials(); // Required for SignalR WebSocket connections
            });
        });

        return services;
    }
}
