using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub") ?? "0");

        /// <summary>POST /api/orders/checkout</summary>
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutDto dto)
        {
            var userId = GetUserId();
            var (success, orderCode, message) = await _orderService.CheckoutAsync(dto, userId > 0 ? userId : null);
            if (!success)
                return BadRequest(new { success = false, message });

            return StatusCode(201, new { success = true, orderCode, message });
        }

        /// <summary>GET /api/orders - Lấy đơn hàng của user đang đăng nhập</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetUserId();
            var orders = await _orderService.GetUserOrdersAsync(userId);
            return Ok(orders);
        }

        /// <summary>GET /api/orders/{code} - Tra cứu đơn hàng theo mã</summary>
        [HttpGet("{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            var userId = GetUserId();
            var order = await _orderService.GetOrderByCodeAsync(code, userId > 0 ? userId : null);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            return Ok(order);
        }

        /// <summary>POST /api/orders/{id}/cancel - Người dùng hủy đơn hàng (khi đang chờ xử lý)</summary>
        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userId = GetUserId();
            var (success, message) = await _orderService.CancelOrderAsync(id, userId);
            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message });
        }
    }
}
