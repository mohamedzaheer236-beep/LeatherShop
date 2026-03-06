using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
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
    }

    /// <summary>Send a simple text message</summary>
    public async Task SendTextMessage(string to, string message, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "text",
            text = new { body = message }
        };
        await SendRequest(payload, ct);
    }

    /// <summary>Send an image message with optional caption</summary>
    public async Task SendImageMessage(string to, string imageUrl, string? caption = null, CancellationToken ct = default)
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
        await SendRequest(payload, ct);
    }

    /// <summary>Send a video message with optional caption</summary>
    public async Task SendVideoMessage(string to, string videoUrl, string? caption = null, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "video",
            video = new
            {
                link = videoUrl,
                caption
            }
        };
        await SendRequest(payload, ct);
    }

    /// <summary>
    /// Send interactive LIST message (menu with sections).
    /// Customer taps a button → sees a scrollable list of items to pick from.
    /// Perfect for: "Browse Categories", "View Products in Wallets", etc.
    /// </summary>
    public async Task SendListMessage(string to, string headerText, string bodyText, string buttonText, List<ListSection> sections, CancellationToken ct = default)
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
        await SendRequest(payload, ct);
    }

    /// <summary>
    /// Send interactive BUTTON message (up to 3 quick reply buttons).
    /// Perfect for: "Add to Cart / View Cart / Main Menu"
    /// </summary>
    public async Task SendButtonMessage(string to, string bodyText, List<ButtonOption> buttons, CancellationToken ct = default)
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
        await SendRequest(payload, ct);
    }

    /// <summary>
    /// Send a template message (required for broadcast / first message to customer).
    /// Template must be pre-approved in Meta Business Manager.
    /// </summary>
    public async Task SendTemplateMessage(string to, string templateName, string languageCode = "en", List<string>? parameters = null, string? imageUrl = null, CancellationToken ct = default)
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
        await SendRequest(payload, ct);
    }

    /// <summary>
    /// Send a carousel template message with dynamic image cards.
    /// Each card has a header image, body text with a parameter, and a quick-reply button.
    /// Template must be pre-approved in Meta Business Manager.
    /// The number of cards MUST match the template definition (e.g., product_gallery has 2 cards).
    /// </summary>
    public async Task SendCarouselTemplateMessage(string to, string templateName, List<CarouselCard> cards, string languageCode = "en", CancellationToken ct = default)
    {
        // Build card components - each card has header (image), body (text param), and button (quick_reply payload)
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
        await SendRequest(payload, ct);
    }

    /// <summary>
    /// Fetch approved message templates from Meta Business API.
    /// Uses the WABA ID to query templates with status=APPROVED.
    /// </summary>
    public async Task<List<WhatsAppTemplate>> GetApprovedTemplates(CancellationToken ct = default)
    {
        var wabaId = _config["WhatsApp:BusinessAccountId"];
        if (string.IsNullOrEmpty(wabaId))
        {
            _logger.LogWarning("WhatsApp:BusinessAccountId not configured. Cannot fetch templates.");
            return new List<WhatsAppTemplate>();
        }

        var url = $"https://graph.facebook.com/{ApiVersion}/{wabaId}/message_templates?status=APPROVED&limit=100";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

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

    private async Task SendRequest(object payload, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        _logger.LogDebug("WhatsApp API Request: {Json}", json);

        for (int attempt = 0; attempt <= RateLimitMaxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("WhatsApp API Success: {Body}", responseBody);
                return;
            }

            // Retry only on rate limit errors (Meta code 131056) - all other errors fail immediately
            if (responseBody.Contains(RateLimitErrorCode) && attempt < RateLimitMaxRetries)
            {
                var delay = RateLimitRetryDelaysMs[attempt];
                _logger.LogWarning("WhatsApp rate limit hit (attempt {Attempt}/{Max}), retrying after {Delay}ms",
                    attempt + 1, RateLimitMaxRetries + 1, delay);
                await Task.Delay(delay, ct);
                continue;
            }

            _logger.LogError("WhatsApp API Error: {StatusCode} - {Body}", response.StatusCode, responseBody);
            throw new WhatsAppApiException($"WhatsApp API Error: {response.StatusCode} - {responseBody}");
        }
    }
}
