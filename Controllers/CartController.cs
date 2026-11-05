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
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub") ?? "0");

        /// <summary>GET /api/cart</summary>
        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var items = await _cartService.GetCartAsync(GetUserId());
            return Ok(items);
        }

        /// <summary>POST /api/cart</summary>
        [HttpPost]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
        {
            var item = await _cartService.AddToCartAsync(GetUserId(), dto);
            if (item == null)
                return BadRequest(new { success = false, message = "Sản phẩm không tồn tại hoặc không đủ hàng" });

            return StatusCode(201, new { success = true, item });
        }

        /// <summary>PUT /api/cart/{id}</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCartItem(int id, [FromBody] UpdateCartDto dto)
        {
            var item = await _cartService.UpdateCartItemAsync(GetUserId(), id, dto);
            if (item == null) return NotFound(new { message = "Không tìm thấy mục trong giỏ hàng" });
            return Ok(new { success = true, item });
        }

        /// <summary>DELETE /api/cart/{id}</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> RemoveCartItem(int id)
        {
            var success = await _cartService.RemoveCartItemAsync(GetUserId(), id);
            if (!success) return NotFound(new { message = "Không tìm thấy mục trong giỏ hàng" });
            return Ok(new { success = true });
        }

        /// <summary>DELETE /api/cart</summary>
        [HttpDelete]
        public async Task<IActionResult> ClearCart()
        {
            await _cartService.ClearCartAsync(GetUserId());
            return Ok(new { success = true });
        }
    }
}
