using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        /// <summary>GET /api/reviews/{productId}</summary>
        [HttpGet("{productId:int}")]
        public async Task<IActionResult> GetProductReviews(int productId)
        {
            var reviews = await _reviewService.GetProductReviewsAsync(productId);
            return Ok(reviews);
        }

        /// <summary>GET /api/reviews/my-reviews</summary>
        [HttpGet("my-reviews")]
        [Authorize]
        public async Task<IActionResult> GetMyReviews()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub") ?? "0");
            var reviews = await _reviewService.GetUserReviewsAsync(userId);
            return Ok(reviews);
        }

        /// <summary>GET /api/reviews/can-review/{productId}</summary>
        [HttpGet("can-review/{productId:int}")]
        [Authorize]
        public async Task<IActionResult> CanReview(int productId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub") ?? "0");
            var (canReview, message, orderId) = await _reviewService.CheckReviewEligibilityAsync(userId, productId);
            return Ok(new { canReview, message, orderId });
        }

        /// <summary>POST /api/reviews</summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub") ?? "0");

            var (success, message, review) = await _reviewService.CreateReviewAsync(userId, dto);
            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message, review });
        }

        /// <summary>POST /api/reviews/comment - Gửi bình luận hỏi đáp (chỉ cần đăng nhập, không cần mua hàng)</summary>
        [HttpPost("comment")]
        [Authorize]
        public async Task<IActionResult> CreateComment([FromBody] CreateCommentDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub") ?? "0");

            var (success, message, comment) = await _reviewService.CreateCommentAsync(userId, dto);
            if (!success)
                return BadRequest(new { success = false, message });

            return Ok(new { success = true, message, comment });
        }
    }
}
