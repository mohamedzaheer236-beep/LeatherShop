using LeatherShopAPI.DTOs.Payment;

namespace LeatherShopAPI.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentPageDto?> GetPaymentPageDataAsync(string orderNumber);
    Task<PaymentVerifyResultDto?> VerifyPaymentAsync(PaymentVerifyDto dto);
}
