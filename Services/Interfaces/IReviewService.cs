using Web.Models;

namespace Web.Services.Interfaces
{
    public interface IReviewService
    {
        Task<List<ReviewDto>> GetProductReviewsAsync(int productId);
        Task<List<ReviewDto>> GetUserReviewsAsync(int userId);
        Task<(bool success, string message, ReviewDto? review)> CreateReviewAsync(int userId, CreateReviewDto dto);
        Task<(bool success, string message, ReviewDto? comment)> CreateCommentAsync(int userId, CreateCommentDto dto);
        Task<(bool canReview, string message, int? orderId)> CheckReviewEligibilityAsync(int userId, int productId);
    }
}
