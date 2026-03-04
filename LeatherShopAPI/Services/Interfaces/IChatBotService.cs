using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

public interface IChatBotService
{
    Task ProcessMessage(Customer customer, string messageType, string? textBody, string? interactiveId, string? interactiveTitle, CancellationToken ct = default);
}
