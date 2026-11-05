using Web.Models;

namespace Web.Services.Interfaces
{
    public interface IOrderService
    {
        Task<(bool success, string? orderCode, string message)> CheckoutAsync(CheckoutDto dto, int? userId);
        Task<List<OrderResponseDto>> GetUserOrdersAsync(int userId);
        Task<OrderResponseDto?> GetOrderByCodeAsync(string code, int? userId);
        Task<(bool success, string message)> CancelOrderAsync(int orderId, int userId);
    }
}
