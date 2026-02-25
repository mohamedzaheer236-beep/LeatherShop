using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// Service to send messages via WhatsApp Cloud API.
/// Supports: text, interactive list (menu), interactive buttons, and template (broadcast).
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppService> _logger;

    private string PhoneNumberId => _config["WhatsApp:PhoneNumberId"]
        ?? throw new InvalidOperationException("WhatsApp:PhoneNumberId not configured. Set it in appsettings or environment variables.");
    private string AccessToken => _config["WhatsApp:AccessToken"]
        ?? throw new InvalidOperationException("WhatsApp:AccessToken not configured. Set it in appsettings or environment variables.");
    private string ApiVersion => _config["WhatsApp:ApiVersion"] ?? "v18.0";

    private string BaseUrl => $"https://graph.facebook.com/{ApiVersion}/{PhoneNumberId}/messages";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WhatsAppService(HttpClient httpClient, IConfiguration config, ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
    }

    /// <summary>Send a simple text message</summary>
    public async Task SendTextMessage(string to, string message)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "text",
            text = new { body = message }
        };
        await SendRequest(payload);
    }

    /// <summary>Send an image message with optional caption</summary>
    public async Task SendImageMessage(string to, string imageUrl, string? caption = null)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "image",
            image = new
            {
                link = imageUrl,
                caption
            }
        };
        await SendRequest(payload);
    }

    /// <summary>
    /// Send interactive LIST message (menu with sections).
    /// Customer taps a button → sees a scrollable list of items to pick from.
    /// Perfect for: "Browse Categories", "View Products in Wallets", etc.
    /// </summary>
    public async Task SendListMessage(string to, string headerText, string bodyText, string buttonText, List<ListSection> sections)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "interactive",
            interactive = new
            {
                type = "list",
                header = new { type = "text", text = headerText },
                body = new { text = bodyText },
                action = new
                {
                    button = buttonText,
                    sections = sections.Select(s => new
                    {
                        title = s.Title,
                        rows = s.Rows.Select(r => new
                        {
                            id = r.Id,
                            title = r.Title,
                            description = r.Description
                        })
                    })
                }
            }
        };
        await SendRequest(payload);
    }

    /// <summary>
    /// Send interactive BUTTON message (up to 3 quick reply buttons).
    /// Perfect for: "Add to Cart / View Cart / Main Menu"
    /// </summary>
    public async Task SendButtonMessage(string to, string bodyText, List<ButtonOption> buttons)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "interactive",
            interactive = new
            {
                type = "button",
                body = new { text = bodyText },
                action = new
                {
                    buttons = buttons.Select(b => new
                    {
                        type = "reply",
                        reply = new { id = b.Id, title = b.Title }
                    })
                }
            }
        };
        await SendRequest(payload);
    }

    /// <summary>
    /// Send a template message (required for broadcast / first message to customer).
    /// Template must be pre-approved in Meta Business Manager.
    /// </summary>
    public async Task SendTemplateMessage(string to, string templateName, string languageCode = "en", List<string>? parameters = null, string? imageUrl = null)
    {
        var components = new List<object>();

        // Optional header image
        if (!string.IsNullOrEmpty(imageUrl))
        {
            components.Add(new
            {
                type = "header",
                parameters = new[]
                {
                    new { type = "image", image = new { link = imageUrl } }
                }
            });
        }

        // Optional body parameters
        if (parameters?.Any() == true)
        {
            components.Add(new
            {
                type = "body",
                parameters = parameters.Select(p => new { type = "text", text = p })
            });
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = languageCode },
                components = components.Any() ? components : null
            }
        };
        await SendRequest(payload);
    }

    /// <summary>
    /// Fetch approved message templates from Meta Business API.
    /// Uses the WABA ID to query templates with status=APPROVED.
    /// </summary>
    public async Task<List<WhatsAppTemplate>> GetApprovedTemplates()
    {
        var wabaId = _config["WhatsApp:BusinessAccountId"];
        if (string.IsNullOrEmpty(wabaId))
        {
            _logger.LogWarning("WhatsApp:BusinessAccountId not configured. Cannot fetch templates.");
            return new List<WhatsAppTemplate>();
        }

        var url = $"https://graph.facebook.com/{ApiVersion}/{wabaId}/message_templates?status=APPROVED&limit=100";
        var response = await _httpClient.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to fetch templates: {Body}", body);
            return new List<WhatsAppTemplate>();
        }

        using var doc = JsonDocument.Parse(body);
        var templates = new List<WhatsAppTemplate>();

        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            foreach (var item in data.EnumerateArray())
            {
                templates.Add(new WhatsAppTemplate
                {
                    Name = item.GetProperty("name").GetString() ?? "",
                    Language = item.GetProperty("language").GetString() ?? "en",
                    Status = item.GetProperty("status").GetString() ?? "",
                    Category = item.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : ""
                });
            }
        }

        return templates;
    }

    private async Task SendRequest(object payload)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        _logger.LogInformation("WhatsApp API Request: {Json}", json);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(BaseUrl, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("WhatsApp API Error: {StatusCode} - {Body}", response.StatusCode, responseBody);
            throw new WhatsAppApiException($"WhatsApp API Error: {response.StatusCode} - {responseBody}");
        }

        _logger.LogInformation("WhatsApp API Success: {Body}", responseBody);
    }
}

// Helper models for building interactive messages
public class ListSection
{
    public string Title { get; set; } = string.Empty;
    public List<ListRow> Rows { get; set; } = new();
}

public class ListRow
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ButtonOption
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class WhatsAppTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
