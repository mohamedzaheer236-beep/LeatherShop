using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

public interface IInvoicePdfService
{
    byte[] GenerateInvoice(Order order);
}
