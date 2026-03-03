using System.Threading;

namespace LeatherShopAPI.Services.Interfaces;

public interface IChatBotService
{
    Task ProcessMessage(string from, string name, string messageType, string? textBody, string? interactiveId, string? interactiveTitle, CancellationToken ct = default);
}
