using System.Threading;
using LeatherShopAPI.DTOs.Payment;

namespace LeatherShopAPI.Services.Interfaces;

public enum PaymentPageResult { NotFound, Expired, Cancelled, Ok }

public interface IPaymentService
{
    Task<(PaymentPageResult Result, PaymentPageDto? Data)> GetPaymentPageDataAsync(string orderNumber, CancellationToken ct = default);
    Task<PaymentVerifyResultDto?> VerifyPaymentAsync(PaymentVerifyDto dto, CancellationToken ct = default);
}
