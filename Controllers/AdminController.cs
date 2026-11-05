using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin,warehouse")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        /// <summary>GET /api/admin/dashboard</summary>
        [HttpGet("dashboard")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetDashboard()
        {
            var data = await _adminService.GetDashboardAsync();
            return Ok(data);
        }

        /// <summary>GET /api/admin/orders?status=pending</summary>
        [HttpGet("orders")]
        public async Task<IActionResult> GetAllOrders([FromQuery] string? status)
        {
            var orders = await _adminService.GetAllOrdersAsync(status);
            return Ok(orders);
        }

        /// <summary>PUT /api/admin/orders/{id}/status</summary>
        [HttpPut("orders/{id:int}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            var success = await _adminService.UpdateOrderStatusAsync(id, dto.Status);
            if (!success) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            return Ok(new { success = true, message = "Cập nhật trạng thái thành công" });
        }

        /// <summary>PUT /api/admin/orders/{id}/confirm-payment - Chỉ Admin hoặc Thủ kho được duyệt</summary>
        [HttpPut("orders/{id:int}/confirm-payment")]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            var adminId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value ?? "0");
            var success = await _adminService.ConfirmPaymentAsync(id, adminId);
            if (!success) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng" });
            return Ok(new { success = true, message = "Đã xác nhận thanh toán thành công!" });
        }

        /// <summary>GET /api/admin/users?search=...&role=...</summary>
        [HttpGet("users")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllUsers([FromQuery] string? search, [FromQuery] string? role)
        {
            var users = await _adminService.GetAllUsersAsync(search, role);
            return Ok(users);
        }

        /// <summary>PUT /api/admin/users/{id}/role</summary>
        [HttpPut("users/{id:int}/role")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateUserRoleDto dto)
        {
            var adminId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value ?? "0");
            var (success, message) = await _adminService.UpdateUserRoleAsync(id, dto.Role, adminId);
            if (!success) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message });
        }

        /// <summary>PUT /api/admin/users/{id}/lock</summary>
        [HttpPut("users/{id:int}/lock")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> SetUserLock(int id, [FromBody] SetUserLockDto dto)
        {
            var adminId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value ?? "0");
            var (success, message) = await _adminService.SetUserLockStatusAsync(id, dto.IsLocked, adminId);
            if (!success) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message });
        }
    }
}
