using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private static readonly SemaphoreSlim _orderStockLock = new(1, 1);

        public OrderService(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<(bool success, string? orderCode, string message)> CheckoutAsync(CheckoutDto dto, int? userId)
        {
            if (dto.Cart == null || !dto.Cart.Any())
                return (false, null, "Giỏ hàng của bạn đang trống");

            var info = dto.CustomerInfo;
            if (string.IsNullOrWhiteSpace(info.Name) || string.IsNullOrWhiteSpace(info.Phone) || string.IsNullOrWhiteSpace(info.Address))
                return (false, null, "Vui lòng nhập đầy đủ thông tin giao hàng");

            // 1. Kiểm tra định dạng số điện thoại Việt Nam (10 số, đầu số hợp lệ)
            var phoneRegex = new Regex(@"^(0|\+84)[3|5|7|8|9][0-9]{8}$");
            if (!phoneRegex.IsMatch(info.Phone.Trim()))
                return (false, null, "Số điện thoại không hợp lệ (Vui lòng nhập số điện thoại Việt Nam gồm 10 chữ số)");

            // Khóa đồng bộ + Database Transaction chống Race Condition / Overselling khi 2 khách đặt cùng 1 sản phẩm cuối cùng cùng lúc
            await _orderStockLock.WaitAsync();
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                // 2. Tra cứu sản phẩm trực tiếp từ DB trong transaction để kiểm tra tồn kho & chống giả mạo giá
                var productIds = dto.Cart.Select(c => c.Id).Distinct().ToList();
                var products = await _context.Products
                    .Include(p => p.Variants)
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                decimal calculatedTotal = 0;
                var orderItems = new List<OrderItem>();

                // Gộp các dòng giỏ hàng trùng (cùng ProductId + Variant + Color) để kiểm tra tổng số lượng chính xác
                var groupedCart = dto.Cart
                    .GroupBy(c => new { c.Id, Variant = c.Variant ?? "", Color = c.Color ?? "" })
                    .Select(g => new CartItemDto
                    {
                        Id = g.Key.Id,
                        Variant = g.First().Variant,
                        Color = g.First().Color,
                        Qty = g.Sum(x => x.Qty)
                    })
                    .ToList();

                foreach (var item in groupedCart)
                {
                    if (!products.TryGetValue(item.Id, out var product) || !product.IsActive)
                    {
                        await transaction.RollbackAsync();
                        return (false, null, $"Sản phẩm ID {item.Id} không tồn tại hoặc đã ngừng kinh doanh");
                    }

                    if (item.Qty <= 0)
                    {
                        await transaction.RollbackAsync();
                        return (false, null, $"Số lượng của sản phẩm '{product.Name}' không hợp lệ");
                    }

                    // Sản phẩm có biến thể: phải chọn đúng phiên bản + màu và trừ tồn kho nguyên tử (Atomic SQL UPDATE)
                    ProductVariant? variant = null;
                    if (product.Variants.Any())
                    {
                        variant = product.Variants.FirstOrDefault(v => v.VariantName == item.Variant && v.ColorName == item.Color);
                        if (variant == null)
                        {
                            await transaction.RollbackAsync();
                            return (false, null, $"Vui lòng chọn phiên bản và màu sắc hợp lệ cho sản phẩm '{product.Name}'");
                        }

                        // Trừ tồn kho nguyên tử trên DB kèm điều kiện StockQuantity >= item.Qty
                        var variantRows = await _context.ProductVariants
                            .Where(v => v.Id == variant.Id && v.StockQuantity >= item.Qty)
                            .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - item.Qty));

                        if (variantRows == 0)
                        {
                            await transaction.RollbackAsync();
                            ProductService.InvalidateCache();
                            return (false, null, $"Rất tiếc! '{product.Name}' ({variant.VariantName} - {variant.ColorName}) vừa hết hàng hoặc không đủ số lượng (chỉ còn {Math.Max(0, variant.StockQuantity)} sản phẩm).");
                        }
                    }

                    // Trừ tổng tồn kho của sản phẩm nguyên tử trên DB kèm điều kiện StockQuantity >= item.Qty
                    var prodRows = await _context.Products
                        .Where(p => p.Id == product.Id && p.StockQuantity >= item.Qty)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - item.Qty));

                    if (prodRows == 0)
                    {
                        await transaction.RollbackAsync();
                        ProductService.InvalidateCache();
                        return (false, null, $"Rất tiếc! Sản phẩm '{product.Name}' vừa được khách hàng khác đặt mua hết hàng.");
                    }

                    // Lấy giá trực tiếp từ database (chống hack giá từ client) - ưu tiên giá theo biến thể
                    var unitPrice = variant != null ? variant.Price : (product.SalePrice > 0 ? product.SalePrice : product.Price);
                    var subtotal = unitPrice * item.Qty;
                    calculatedTotal += subtotal;

                    orderItems.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity  = item.Qty,
                        UnitPrice = unitPrice,
                        Subtotal  = subtotal,
                        Variant   = item.Variant,
                        Color     = item.Color
                    });
                }

                // 3. Kiểm tra mã giảm giá (nếu có) bằng cập nhật nguyên tử
                decimal discountValue = 0;
                if (!string.IsNullOrWhiteSpace(dto.PromotionCode))
                {
                    var promoCode = dto.PromotionCode.Trim().ToUpper();
                    var promo = await _context.Promotions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Code == promoCode && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow);

                    if (promo != null)
                    {
                        if (calculatedTotal < promo.MinOrderValue)
                        {
                            await transaction.RollbackAsync();
                            return (false, null, $"Đơn hàng tối thiểu {promo.MinOrderValue:N0}₫ để áp dụng mã '{promoCode}'");
                        }

                        var promoRows = await _context.Promotions
                            .Where(p => p.Id == promo.Id && (p.MaxUsage <= 0 || p.UsedCount < p.MaxUsage))
                            .ExecuteUpdateAsync(s => s.SetProperty(p => p.UsedCount, p => p.UsedCount + 1));

                        if (promoRows == 0)
                        {
                            await transaction.RollbackAsync();
                            return (false, null, $"Mã giảm giá '{promoCode}' vừa hết lượt sử dụng");
                        }

                        if (promo.DiscountType == "percent")
                            discountValue = calculatedTotal * (promo.DiscountValue / 100m);
                        else
                            discountValue = promo.DiscountValue;
                    }
                }

                var finalAmount = Math.Max(0, calculatedTotal - discountValue);
                var orderCode = "TS" + DateTime.UtcNow.ToString("yyyyMMdd") + Guid.NewGuid().ToString()[..4].ToUpper();

                var order = new Order
                {
                    UserId          = userId,
                    OrderCode       = orderCode,
                    ShippingAddress = $"{info.Address}, {info.District}, {info.City}".Trim(',', ' '),
                    PaymentMethod   = info.PaymentMethod,
                    PaymentStatus   = "unpaid",
                    Status          = "pending",
                    TotalAmount     = finalAmount,
                    OrderItems      = orderItems
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Xóa giỏ hàng trên server nếu user đã đăng nhập
                if (userId.HasValue)
                {
                    await _context.Carts.Where(c => c.UserId == userId.Value).ExecuteDeleteAsync();
                }

                await transaction.CommitAsync();
                ProductService.InvalidateCache();

                // Gửi email xác nhận mô phỏng (không chặn transaction)
                _ = _emailService.SendOrderConfirmationEmailAsync(info.Email, orderCode, finalAmount);

                return (true, orderCode, "Đặt hàng thành công");
            }
            finally
            {
                _orderStockLock.Release();
            }
        }

        public async Task<List<OrderResponseDto>> GetUserOrdersAsync(int userId)
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Variants)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderedAt)
                .ToListAsync();

            return orders.Select(MapToDto).ToList();
        }

        public async Task<OrderResponseDto?> GetOrderByCodeAsync(string code, int? userId)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Images)
                .Where(o => o.OrderCode == code);

            // Bảo mật: Người dùng thông thường chỉ xem được đơn hàng của chính mình
            if (userId.HasValue)
                query = query.Where(o => o.UserId == userId || o.UserId == null);

            var order = await query.FirstOrDefaultAsync();
            return order == null ? null : MapToDto(order);
        }

        public async Task<(bool success, string message)> CancelOrderAsync(int orderId, int userId)
        {
            await _orderStockLock.WaitAsync();
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p!.Variants)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

                if (order == null)
                    return (false, "Không tìm thấy đơn hàng");

                if (order.Status != "pending")
                    return (false, "Chỉ có thể hủy đơn hàng đang ở trạng thái 'Chờ xử lý'");

                order.Status = "cancelled";

                // Hoàn lại số lượng tồn kho cho các sản phẩm trong đơn
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.StockQuantity += item.Quantity;
                        var v = item.Product.Variants.FirstOrDefault(x => x.VariantName == item.Variant && x.ColorName == item.Color);
                        if (v != null) v.StockQuantity += item.Quantity;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                ProductService.InvalidateCache();
                return (true, "Đã hủy đơn hàng và hoàn lại số lượng tồn kho thành công");
            }
            finally
            {
                _orderStockLock.Release();
            }
        }

        private static OrderResponseDto MapToDto(Order o) => new()
        {
            Id              = o.Id,
            OrderCode       = o.OrderCode,
            TotalAmount     = o.TotalAmount,
            Status          = o.Status,
            PaymentMethod   = o.PaymentMethod,
            PaymentStatus   = o.PaymentStatus,
            ShippingAddress = o.ShippingAddress,
            OrderedAt       = o.OrderedAt,
            Items           = o.OrderItems.Select(i => new OrderItemResponseDto
            {
                ProductId    = i.ProductId,
                ProductName  = i.Product?.Name ?? "",
                ProductImage = i.Product?.Variants.FirstOrDefault(v => v.VariantName == i.Variant && v.ColorName == i.Color)?.ImageUrl
                               ?? i.Product?.Images.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                               ?? i.Product?.Images.FirstOrDefault()?.ImageUrl ?? "",
                Quantity     = i.Quantity,
                UnitPrice    = i.UnitPrice,
                Subtotal     = i.Subtotal,
                Variant      = i.Variant,
                Color        = i.Color
            }).ToList()
        };
    }
}

