using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Models;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PromotionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PromotionsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("check")]
        public async Task<IActionResult> CheckPromotion([FromQuery] string code)
        {
            if (string.IsNullOrEmpty(code)) return BadRequest("Mã không hợp lệ");

            var promo = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Code == code && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow);

            if (promo == null)
                return NotFound(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã hết hạn" });

            if (promo.UsedCount >= promo.MaxUsage)
                return BadRequest(new { success = false, message = "Mã giảm giá đã hết lượt sử dụng" });

            return Ok(new
            {
                success = true,
                discountType = promo.DiscountType,
                discountValue = promo.DiscountValue,
                minOrderValue = promo.MinOrderValue
            });
        }
    }
}
