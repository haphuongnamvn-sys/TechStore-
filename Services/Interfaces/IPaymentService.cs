using Web.Models;

namespace Web.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<object> CreatePaymentAsync(CreatePaymentDto dto);
        Task<bool> ProcessCallbackAsync(PaymentCallbackDto dto);
    }
}
