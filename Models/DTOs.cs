using System.ComponentModel.DataAnnotations;

namespace Web.Models
{
    // Auth DTOs public class UserRegisterDto
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
        [MinLength(3, ErrorMessage = "Tên đăng nhập phải có ít nhất 3 ký tự")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        public string Password { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;
    }

    public class UserLoginDto
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        public string Password { get; set; } = string.Empty;
    }

    public class UserProfileDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateProfileDto
    {
        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Mật khẩu hiện tại không được để trống")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UpdateUserRoleDto
    {
        [Required]
        public string Role { get; set; } = "customer"; // customer | admin | warehouse
    }

    public class SetUserLockDto
    {
        public bool IsLocked { get; set; }
    }

    // Category DTOs public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public bool IsActive { get; set; } = true;
        public int ProductCount { get; set; }
        public List<string> SpecTemplates { get; set; } = new();
    }

    public class SaveCategoryDto
    {
        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Slug { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string>? SpecTemplates { get; set; }
    }

    // Product DTOs public class ProductListDto
    {
        public int Id { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal SalePrice { get; set; }
        public string Badge { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public double RatingAvg { get; set; }
        public int ReviewCount { get; set; }
        public int StockQuantity { get; set; }
        public string PrimaryImage { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string CategorySlug { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int BrandId { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> Images { get; set; } = new();
        public List<string> Highlights { get; set; } = new();
        public List<ProductSpecDto> Specs { get; set; } = new();
        public List<ProductVariantDto> Variants { get; set; } = new();
    }

    public class ProductDetailDto : ProductListDto
    {
        public List<ReviewDto> Reviews { get; set; } = new();
    }

    public class ProductVariantDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string VariantName { get; set; } = string.Empty;
        public string ColorName { get; set; } = string.Empty;
        public string? ColorCode { get; set; }
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int StockQuantity { get; set; }
        public string? Sku { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class ProductSpecDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class CreateProductDto
    {
        public string? ProductCode { get; set; }

        [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Giá gốc không được để trống")]
        public decimal Price { get; set; }
        
        public decimal SalePrice { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Badge { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int StockQuantity { get; set; }
        
        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        public int CategoryId { get; set; }
        
        [Required(ErrorMessage = "Vui lòng chọn thương hiệu")]
        public int BrandId { get; set; }
        
        public string PrimaryImage { get; set; } = string.Empty;
        public List<string>? Images { get; set; }
        public List<string>? Highlights { get; set; }
        public List<ProductVariantDto>? Variants { get; set; }
        public List<ProductSpecDto>? Specs { get; set; }
    }

    public class UpdateProductDto : CreateProductDto
    {
        // Inherits all fields from CreateProductDto
    }

    public class UpdateStockDto
    {
        [Required(ErrorMessage = "Số lượng không được để trống")]
        public int StockQuantity { get; set; }
        public int? VariantId { get; set; }
    }

    // Cart DTOs public class AddToCartDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, 100, ErrorMessage = "Số lượng phải từ 1 đến 100")]
        public int Quantity { get; set; }

        public string? Variant { get; set; }
        public string? Color { get; set; }
        public decimal? CustomPrice { get; set; }
    }

    public class UpdateCartDto
    {
        [Required]
        [Range(1, 100, ErrorMessage = "Số lượng phải từ 1 đến 100")]
        public int Quantity { get; set; }
    }

    public class CartItemResponseDto
    {
        public int CartItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal SalePrice { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
        public string? Variant { get; set; }
        public string? Color { get; set; }
    }

    // Order DTOs public class CheckoutDto
    {
        [Required]
        public CustomerInfoDto CustomerInfo { get; set; } = null!;

        [Required]
        [MinLength(1, ErrorMessage = "Giỏ hàng không được rỗng")]
        public List<CartItemDto> Cart { get; set; } = new();

        [Range(0, double.MaxValue, ErrorMessage = "Tổng tiền không hợp lệ")]
        public decimal TotalAmount { get; set; }

        public string? PromotionCode { get; set; }
    }

    public class CustomerInfoDto
    {
        [Required(ErrorMessage = "Tên không được để trống")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;

        [Required]
        public string District { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;

        [Required]
        public string PaymentMethod { get; set; } = string.Empty; // cod | online
    }

    public class CartItemDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public int Qty { get; set; }

        public string? Variant { get; set; }
        public string? Color { get; set; }
    }

    public class OrderResponseDto
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public DateTime OrderedAt { get; set; }
        public List<OrderItemResponseDto> Items { get; set; } = new();
    }

    public class OrderItemResponseDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string? Variant { get; set; }
        public string? Color { get; set; }
    }

    // Payment DTOs public class CreatePaymentDto
    {
        [Required]
        public string OrderCode { get; set; } = string.Empty;

        [Required]
        public string Provider { get; set; } = "vnpay"; // vnpay | momo | stripe
    }

    public class PaymentCallbackDto
    {
        public string TransactionId { get; set; } = string.Empty;
        public string OrderCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // success | failed
        public decimal Amount { get; set; }
    }

    // Review DTOs public class CreateReviewDto
    {
        [Required]
        public int ProductId { get; set; }

        public int? OrderId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao")]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string Comment { get; set; } = string.Empty;
    }

    public class CreateCommentDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Nội dung bình luận không được để trống")]
        [MaxLength(3000, ErrorMessage = "Bình luận tối đa 3000 ký tự")]
        public string Content { get; set; } = string.Empty;
    }

    public class ReviewDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int? OrderId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    // Admin DTOs public class DashboardDto
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalUsers { get; set; }
        public int TotalProducts { get; set; }
        public int PendingOrders { get; set; }
        public decimal Revenue7Days { get; set; }
        public List<RevenueByDayDto> RevenueByDay { get; set; } = new();
    }

    public class RevenueByDayDto
    {
        public string Date { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    public class UpdateOrderStatusDto
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }

    // Chat DTOs public class ChatRequestDto
    {
        [Required(ErrorMessage = "Tin nhắn không được rỗng")]
        public string Message { get; set; } = string.Empty;

        public string? SessionId { get; set; }

        public string Target { get; set; } = "bot"; // "bot" | "staff"
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Sender { get; set; } = "customer"; // "customer" | "staff" | "bot"
        public string SenderName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsStaffReply { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class StaffReplyDto
    {
        [Required]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;
    }

    public class ConversationSummaryDto
    {
        public string SessionId { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string CustomerName { get; set; } = "Khách vãng lai";
        public string CustomerPhone { get; set; } = string.Empty;
        public string LastMessage { get; set; } = string.Empty;
        public string LastSender { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
        public int TotalMessages { get; set; }
        public int? AssignedStaffId { get; set; }
        public string? AssignedStaffName { get; set; }
        public string Status { get; set; } = "waiting"; // "waiting" | "in_progress" | "closed"
    }

    public class TransferConversationDto
    {
        public int? TargetStaffId { get; set; }
        public string? Note { get; set; }
    }

    public class SupportStaffDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class MergeGuestSessionDto
    {
        public string GuestSessionId { get; set; } = string.Empty;
    }
}
