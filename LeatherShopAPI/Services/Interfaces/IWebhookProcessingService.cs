using System.Threading;
using LeatherShopAPI.DTOs.WhatsApp;

namespace LeatherShopAPI.Services.Interfaces;

public interface IWebhookProcessingService
{
    /// <summary>
    /// Processes a deserialized WhatsApp webhook payload:
    /// resolves customers, saves chat messages, pushes SignalR notifications,
    /// and delegates to the chatbot for automated responses.
    /// </summary>
    Task ProcessWebhookPayloadAsync(WhatsAppWebhookPayload payload, CancellationToken ct = default);
}
