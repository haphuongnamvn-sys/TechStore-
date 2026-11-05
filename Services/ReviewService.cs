using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Services
{
    public class ReviewService : IReviewService
    {
        private readonly AppDbContext _context;

        public ReviewService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReviewDto>> GetProductReviewsAsync(int productId)
        {
            return await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDto
                {
                    Id        = r.Id,
                    ProductId = r.ProductId,
                    OrderId   = r.OrderId,
                    Username  = r.User != null ? r.User.FullName : "Ẩn danh",
                    Rating    = r.Rating,
                    Comment   = r.Comment,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<ReviewDto>> GetUserReviewsAsync(int userId)
        {
            return await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDto
                {
                    Id        = r.Id,
                    ProductId = r.ProductId,
                    OrderId   = r.OrderId,
                    Username  = r.User != null ? r.User.FullName : "Ẩn danh",
                    Rating    = r.Rating,
                    Comment   = r.Comment,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<(bool canReview, string message, int? orderId)> CheckReviewEligibilityAsync(int userId, int productId)
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.OrderItems.Any(oi => oi.ProductId == productId))
                .OrderByDescending(o => o.OrderedAt)
                .ToListAsync();

            if (!orders.Any())
            {
                return (false, "Bạn chưa mua sản phẩm này. Chỉ những khách hàng đã mua và nhận hàng thành công mới có thể đánh giá.", null);
            }

            var deliveredOrder = orders.FirstOrDefault(o => o.Status == "delivered");
            if (deliveredOrder == null)
            {
                var latest = orders.First();
                var statusText = latest.Status switch
                {
                    "pending" => "đang chờ xác nhận",
                    "processing" => "đang chuẩn bị hàng",
                    "shipped" => "đang được giao",
                    "cancelled" => "đã hủy",
                    _ => latest.Status
                };
                return (false, $"Đơn hàng của bạn {statusText}. Bạn chỉ có thể đánh giá sau khi đơn hàng được giao thành công (hoàn thành).", null);
            }

            return (true, "Đủ điều kiện đánh giá", deliveredOrder.Id);
        }

        public async Task<(bool success, string message, ReviewDto? review)> CreateReviewAsync(int userId, CreateReviewDto dto)
        {
            if (dto.Rating < 1 || dto.Rating > 5)
            {
                return (false, "Số sao đánh giá phải từ 1 đến 5.", null);
            }

            int? targetOrderId = dto.OrderId;

            if (targetOrderId.HasValue)
            {
                var order = await _context.Orders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == targetOrderId.Value && o.UserId == userId && o.OrderItems.Any(oi => oi.ProductId == dto.ProductId));

                if (order == null)
                {
                    return (false, "Không tìm thấy đơn hàng hợp lệ chứa sản phẩm này.", null);
                }

                if (order.Status != "delivered")
                {
                    return (false, "Bạn chỉ có thể đánh giá sản phẩm khi đơn hàng đã hoàn thành (giao thành công).", null);
                }
            }
            else
            {
                var (canReview, msg, orderId) = await CheckReviewEligibilityAsync(userId, dto.ProductId);
                if (!canReview)
                {
                    return (false, msg, null);
                }
                targetOrderId = orderId;
            }

            var existing = await _context.Reviews
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == dto.ProductId && (targetOrderId == null || r.OrderId == targetOrderId));
            
            if (existing != null)
            {
                existing.Rating = dto.Rating;
                existing.Comment = dto.Comment;
                existing.CreatedAt = DateTime.UtcNow;
                if (targetOrderId != null) existing.OrderId = targetOrderId;
                await _context.SaveChangesAsync();

                var avg = await _context.Reviews
                    .Where(r => r.ProductId == dto.ProductId && r.Rating > 0)
                    .AverageAsync(r => (double)r.Rating);

                var p = await _context.Products.FindAsync(dto.ProductId);
                if (p != null)
                {
                    p.RatingAvg = Math.Round(avg, 1);
                    await _context.SaveChangesAsync();
                }
                ProductService.InvalidateCache();

                var u = await _context.Users.FindAsync(userId);
                var updatedDto = new ReviewDto
                {
                    Id        = existing.Id,
                    ProductId = existing.ProductId,
                    OrderId   = existing.OrderId,
                    Username  = u?.FullName ?? "Ẩn danh",
                    Rating    = existing.Rating,
                    Comment   = existing.Comment,
                    CreatedAt = existing.CreatedAt
                };
                return (true, "Đánh giá sản phẩm thành công!", updatedDto);
            }

            var review = new Review
            {
                UserId    = userId,
                ProductId = dto.ProductId,
                OrderId   = targetOrderId,
                Rating    = dto.Rating,
                Comment   = dto.Comment,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Recalculate product rating average (chỉ tính review có rating > 0)
            var avgRating = await _context.Reviews
                .Where(r => r.ProductId == dto.ProductId && r.Rating > 0)
                .AverageAsync(r => (double)r.Rating);

            var product = await _context.Products.FindAsync(dto.ProductId);
            if (product != null)
            {
                product.RatingAvg = Math.Round(avgRating, 1);
                await _context.SaveChangesAsync();
            }
            ProductService.InvalidateCache();

            var user = await _context.Users.FindAsync(userId);
            var resultDto = new ReviewDto
            {
                Id        = review.Id,
                ProductId = review.ProductId,
                OrderId   = review.OrderId,
                Username  = user?.FullName ?? "Ẩn danh",
                Rating    = review.Rating,
                Comment   = review.Comment,
                CreatedAt = review.CreatedAt
            };
            return (true, "Đánh giá sản phẩm thành công!", resultDto);
        }

        public async Task<(bool success, string message, ReviewDto? comment)> CreateCommentAsync(int userId, CreateCommentDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Content))
            {
                return (false, "Nội dung bình luận không được để trống.", null);
            }

            var product = await _context.Products.FindAsync(dto.ProductId);
            if (product == null)
            {
                return (false, "Không tìm thấy sản phẩm.", null);
            }

            var comment = new Review
            {
                UserId    = userId,
                ProductId = dto.ProductId,
                OrderId   = null,
                Rating    = 0, // 0 biểu thị đây là bình luận / hỏi đáp, không phải đánh giá có sao
                Comment   = dto.Content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(comment);
            await _context.SaveChangesAsync();
            ProductService.InvalidateCache();

            var user = await _context.Users.FindAsync(userId);
            var resultDto = new ReviewDto
            {
                Id        = comment.Id,
                ProductId = comment.ProductId,
                OrderId   = null,
                Username  = user?.FullName ?? user?.Username ?? "Ẩn danh",
                Rating    = 0,
                Comment   = comment.Comment,
                CreatedAt = comment.CreatedAt
            };

            return (true, "Gửi bình luận thành công!", resultDto);
        }
    }
}
