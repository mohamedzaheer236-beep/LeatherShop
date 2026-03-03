using LeatherShopAPI.DTOs.Payment;

namespace LeatherShopAPI.Services.Interfaces;

public enum PaymentPageResult { NotFound, Expired, Ok }

public interface IPaymentService
{
    Task<(PaymentPageResult Result, PaymentPageDto? Data)> GetPaymentPageDataAsync(string orderNumber);
    Task<PaymentVerifyResultDto?> VerifyPaymentAsync(PaymentVerifyDto dto);
}
