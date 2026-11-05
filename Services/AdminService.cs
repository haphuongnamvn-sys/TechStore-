using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Services
{
    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;

        public AdminService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardDto> GetDashboardAsync()
        {
            var totalOrders   = await _context.Orders.CountAsync(o => o.Status != "cancelled");
            var totalRevenue  = await _context.Orders
                .Where(o => o.Status != "cancelled" && o.PaymentStatus == "paid")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            var totalUsers    = await _context.Users.CountAsync(u => u.Role == "customer" || string.IsNullOrEmpty(u.Role));
            var totalProducts = await _context.Products.CountAsync();
            var pendingOrders = await _context.Orders.CountAsync(o => o.Status == "pending");

            // Revenue by day (last 7 days - chỉ tính đơn đã xác nhận thanh toán)
            var today = DateTime.UtcNow.Date;
            var last7Days = today.AddDays(-6);

            var ordersLast7Days = await _context.Orders
                .AsNoTracking()
                .Where(o => o.OrderedAt >= last7Days && o.Status != "cancelled" && o.PaymentStatus == "paid")
                .Select(o => new { o.OrderedAt, o.TotalAmount })
                .ToListAsync();

            var ordersByDate = ordersLast7Days
                .GroupBy(o => o.OrderedAt.Date)
                .ToDictionary(
                    g => g.Key,
                    g => new { Revenue = g.Sum(o => o.TotalAmount), Count = g.Count() }
                );

            var revenueByDay = Enumerable.Range(0, 7)
                .Select(offset => last7Days.AddDays(offset))
                .Select(date =>
                {
                    ordersByDate.TryGetValue(date, out var item);
                    return new RevenueByDayDto
                    {
                        Date       = date.ToString("dd/MM"),
                        Revenue    = item?.Revenue ?? 0,
                        OrderCount = item?.Count ?? 0
                    };
                })
                .ToList();

            var revenue7Days = revenueByDay.Sum(r => r.Revenue);

            return new DashboardDto
            {
                TotalOrders   = totalOrders,
                TotalRevenue  = totalRevenue,
                TotalUsers    = totalUsers,
                TotalProducts = totalProducts,
                PendingOrders = pendingOrders,
                Revenue7Days  = revenue7Days,
                RevenueByDay  = revenueByDay
            };
        }

        public async Task<List<OrderResponseDto>> GetAllOrdersAsync(string? status = null)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Images)
                .Include(o => o.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            var orders = await query.OrderByDescending(o => o.OrderedAt).ToListAsync();

            return orders.Select(o => new OrderResponseDto
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
                    ProductImage = i.Product?.Images.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                                   ?? i.Product?.Images.FirstOrDefault()?.ImageUrl ?? "",
                    Quantity     = i.Quantity,
                    UnitPrice    = i.UnitPrice,
                    Subtotal     = i.Subtotal,
                    Variant      = i.Variant,
                    Color        = i.Color
                }).ToList()
            }).ToList();
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Variants)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return false;

            var oldStatus = order.Status;
            order.Status = status;

            // Nếu chuyển sang trạng thái Đã hủy từ trạng thái chưa hủy -> hoàn lại tồn kho
            if (status == "cancelled" && oldStatus != "cancelled")
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.StockQuantity += item.Quantity;
                        var v = item.Product.Variants.FirstOrDefault(x => x.VariantName == item.Variant && x.ColorName == item.Color);
                        if (v != null) v.StockQuantity += item.Quantity;
                    }
                }
            }

            await _context.SaveChangesAsync();
            ProductService.InvalidateCache();
            return true;
        }

        public async Task<bool> ConfirmPaymentAsync(int orderId, int adminId)
        {
            var order = await _context.Orders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return false;

            order.PaymentStatus = "paid";
            if (order.Status == "pending")
            {
                order.Status = "processing";
            }

            var payment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
            if (payment == null)
            {
                payment = new Payment
                {
                    OrderId       = order.Id,
                    Amount        = order.TotalAmount,
                    Provider      = order.PaymentMethod == "cod" ? "cod" : "banking",
                    TransactionId = "TXN" + Guid.NewGuid().ToString()[..8].ToUpper(),
                    Status        = "success",
                    PaidAt        = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
            }
            else
            {
                payment.Status = "success";
                payment.PaidAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<UserProfileDto>> GetAllUsersAsync(string? search = null, string? role = null)
        {
            var query = _context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(s) ||
                                         u.FullName.ToLower().Contains(s) ||
                                         u.Email.ToLower().Contains(s) ||
                                         u.Phone.ToLower().Contains(s));
            }

            return await query
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new UserProfileDto
                {
                    Id        = u.Id,
                    Username  = u.Username,
                    FullName  = u.FullName,
                    Email     = u.Email,
                    Phone     = u.Phone,
                    Address   = u.Address,
                    Role      = u.Role,
                    IsLocked  = u.IsLocked,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<(bool success, string message)> UpdateUserRoleAsync(int targetUserId, string newRole, int currentAdminId)
        {
            var validRoles = new[] { "customer", "admin", "warehouse", "support" };
            if (!validRoles.Contains(newRole.ToLower()))
                return (false, "Quyền không hợp lệ!");

            var target = await _context.Users.FindAsync(targetUserId);
            if (target == null) return (false, "Không tìm thấy người dùng!");

            if (target.Id == currentAdminId && newRole.ToLower() != "admin")
                return (false, "Bạn không thể tự hạ quyền Admin của chính mình!");

            target.Role = newRole.ToLower();
            await _context.SaveChangesAsync();
            return (true, $"Đã cập nhật quyền thành '{newRole}' cho người dùng {target.Username}!");
        }

        public async Task<(bool success, string message)> SetUserLockStatusAsync(int targetUserId, bool isLocked, int currentAdminId)
        {
            var target = await _context.Users.FindAsync(targetUserId);
            if (target == null) return (false, "Không tìm thấy người dùng!");

            if (target.Id == currentAdminId)
                return (false, "Bạn không thể tự khóa tài khoản của chính mình!");

            target.IsLocked = isLocked;
            await _context.SaveChangesAsync();
            var actionText = isLocked ? "khóa" : "mở khóa";
            return (true, $"Đã {actionText} tài khoản {target.Username} thành công!");
        }
    }
}
