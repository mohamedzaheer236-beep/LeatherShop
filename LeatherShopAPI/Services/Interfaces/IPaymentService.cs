using LeatherShopAPI.DTOs.Payment;

namespace LeatherShopAPI.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentPageDto?> GetPaymentPageDataAsync(int orderId);
    Task<PaymentVerifyResultDto?> VerifyPaymentAsync(PaymentVerifyDto dto);
}
