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
    private const int RateLimitMaxRetries = 2;
    private static readonly int[] RateLimitRetryDelaysMs = [2000, 5000];
    private const string RateLimitErrorCode = "131056"; // Meta error: (Business, Consumer) pair rate limit hit

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppService> _logger;

    private string PhoneNumberId => _config["WhatsApp:PhoneNumberId"]
        ?? throw new InvalidOperationException("WhatsApp:PhoneNumberId not configured. Set it in appsettings or environment variables.");
    private string AccessToken => _config["WhatsApp:AccessToken"]
        ?? throw new InvalidOperationException("WhatsApp:AccessToken not configured. Set it in appsettings or environment variables.");
    private string ApiVersion => _config["WhatsApp:ApiVersion"] ?? "v21.0";

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
    /// Send a carousel template message with dynamic image cards.
    /// Each card has a header image, body text with a parameter, and a quick-reply button.
    /// Template must be pre-approved in Meta Business Manager.
    /// The number of cards MUST match the template definition (e.g., product_gallery has 2 cards).
    /// </summary>
    public async Task SendCarouselTemplateMessage(string to, string templateName, List<CarouselCard> cards, string languageCode = "en")
    {
        // Build card components — each card has header (image), body (text param), and button (quick_reply payload)
        var carouselCards = cards.Select((card, idx) => new Dictionary<string, object>
        {
            ["card_index"] = idx,
            ["components"] = new object[]
            {
                new { type = "header", parameters = new[] { new { type = "image", image = new { link = card.ImageUrl } } } },
                new { type = "body", parameters = new[] { new { type = "text", text = card.BodyParam } } },
                new { type = "button", sub_type = "quick_reply", index = 0, parameters = new[] { new { type = "payload", payload = card.ButtonPayload } } }
            }
        }).ToList<object>();

        var payload = new Dictionary<string, object>
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = to,
            ["type"] = "template",
            ["template"] = new Dictionary<string, object>
            {
                ["name"] = templateName,
                ["language"] = new { code = languageCode },
                ["components"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "carousel",
                        ["cards"] = carouselCards
                    }
                }
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
        using var response = await _httpClient.GetAsync(url);
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
                var isCarousel = false;
                var cardCount = 0;
                var hasImageHeader = false;
                var bodyParamCount = 0;
                var cardBodyStaticLength = 0; // length of card body text minus placeholders

                if (item.TryGetProperty("components", out var components))
                {
                    foreach (var comp in components.EnumerateArray())
                    {
                        var compTypeStr = comp.TryGetProperty("type", out var compType)
                            ? compType.GetString() ?? ""
                            : "";

                        if (compTypeStr.Equals("CAROUSEL", StringComparison.OrdinalIgnoreCase))
                        {
                            isCarousel = true;
                            if (comp.TryGetProperty("cards", out var cards))
                            {
                                cardCount = cards.GetArrayLength();
                                // Parse the first card's BODY text to measure static length
                                if (cardCount > 0)
                                {
                                    var firstCard = cards[0];
                                    if (firstCard.TryGetProperty("components", out var cardComps))
                                    {
                                        foreach (var cc in cardComps.EnumerateArray())
                                        {
                                            var ccType = cc.TryGetProperty("type", out var cct) ? cct.GetString() ?? "" : "";
                                            if (ccType.Equals("BODY", StringComparison.OrdinalIgnoreCase) && cc.TryGetProperty("text", out var cbText))
                                            {
                                                var raw = cbText.GetString() ?? "";
                                                // Remove placeholders like {{1}} to get static text length
                                                var stripped = System.Text.RegularExpressions.Regex.Replace(raw, @"\{\{\d+\}\}", "");
                                                cardBodyStaticLength = stripped.Length;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (compTypeStr.Equals("HEADER", StringComparison.OrdinalIgnoreCase))
                        {
                            if (comp.TryGetProperty("format", out var fmt) &&
                                fmt.GetString()?.Equals("IMAGE", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                hasImageHeader = true;
                            }
                        }
                        else if (compTypeStr.Equals("BODY", StringComparison.OrdinalIgnoreCase))
                        {
                            if (comp.TryGetProperty("text", out var bodyText))
                            {
                                var text = bodyText.GetString() ?? "";
                                // Count {{1}}, {{2}}, etc. placeholders
                                var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\{\{\d+\}\}");
                                bodyParamCount = matches.Count;
                            }
                        }
                    }
                }
                templates.Add(new WhatsAppTemplate
                {
                    Name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    Language = item.TryGetProperty("language", out var l) ? l.GetString() ?? "en" : "en",
                    Status = item.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "",
                    Category = item.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "",
                    IsCarousel = isCarousel,
                    CardCount = cardCount,
                    HasImageHeader = hasImageHeader,
                    BodyParamCount = bodyParamCount,
                    CardBodyMaxLength = isCarousel && cardBodyStaticLength > 0
                        ? Math.Max(160 - cardBodyStaticLength, 20) // at least 20 chars for the param
                        : (isCarousel ? 120 : 0) // fallback if parsing failed
                });
            }
        }

        return templates;
    }

    private async Task SendRequest(object payload)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        _logger.LogDebug("WhatsApp API Request: {Json}", json);

        for (int attempt = 0; attempt <= RateLimitMaxRetries; attempt++)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(BaseUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("WhatsApp API Success: {Body}", responseBody);
                return;
            }

            // Retry only on rate limit errors (Meta code 131056) — all other errors fail immediately
            if (responseBody.Contains(RateLimitErrorCode) && attempt < RateLimitMaxRetries)
            {
                var delay = RateLimitRetryDelaysMs[attempt];
                _logger.LogWarning("WhatsApp rate limit hit (attempt {Attempt}/{Max}), retrying after {Delay}ms",
                    attempt + 1, RateLimitMaxRetries + 1, delay);
                await Task.Delay(delay);
                continue;
            }

            _logger.LogError("WhatsApp API Error: {StatusCode} - {Body}", response.StatusCode, responseBody);
            throw new WhatsAppApiException($"WhatsApp API Error: {response.StatusCode} - {responseBody}");
        }
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
    /// <summary>True if the template contains a CAROUSEL component.</summary>
    public bool IsCarousel { get; set; }
    /// <summary>Number of cards defined in the carousel (0 for non-carousel templates).</summary>
    public int CardCount { get; set; }
    /// <summary>True if the template has a HEADER component with IMAGE format.</summary>
    public bool HasImageHeader { get; set; }
    /// <summary>Number of body parameters expected (e.g., 2 means {{1}} and {{2}}).</summary>
    public int BodyParamCount { get; set; }
    /// <summary>Max characters allowed for a carousel card body parameter (160 - static text length). 0 for non-carousel.</summary>
    public int CardBodyMaxLength { get; set; }
}

public class CarouselCard
{
    public string ImageUrl { get; set; } = string.Empty;
    public string BodyParam { get; set; } = string.Empty;
    public string ButtonPayload { get; set; } = string.Empty;
}
