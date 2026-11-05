using Web.Models;

namespace Web.Services.Interfaces
{
    public interface IAdminService
    {
        Task<DashboardDto> GetDashboardAsync();
        Task<List<OrderResponseDto>> GetAllOrdersAsync(string? status = null);
        Task<bool> UpdateOrderStatusAsync(int orderId, string status);
        Task<bool> ConfirmPaymentAsync(int orderId, int adminId);
        Task<List<UserProfileDto>> GetAllUsersAsync(string? search = null, string? role = null);
        Task<(bool success, string message)> UpdateUserRoleAsync(int targetUserId, string newRole, int currentAdminId);
        Task<(bool success, string message)> SetUserLockStatusAsync(int targetUserId, bool isLocked, int currentAdminId);
    }
}
