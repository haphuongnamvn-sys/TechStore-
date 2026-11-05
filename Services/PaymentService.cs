using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;

        public PaymentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<object> CreatePaymentAsync(CreatePaymentDto dto)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == dto.OrderCode);

            if (order == null)
                return new { success = false, message = "Không tìm thấy đơn hàng" };

            if (order.PaymentStatus == "paid")
                return new { success = false, message = "Đơn hàng này đã được thanh toán trước đó" };

            // Create a pending payment record
            var payment = new Payment
            {
                OrderId       = order.Id,
                Amount        = order.TotalAmount,
                Provider      = dto.Provider,
                Status        = "pending",
                TransactionId = "TXN" + Guid.NewGuid().ToString()[..8].ToUpper()
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            var paymentUrl = $"/payment-gateway?txn={payment.TransactionId}&amount={order.TotalAmount}&order={order.OrderCode}&provider={dto.Provider}";

            return new
            {
                success       = true,
                transactionId = payment.TransactionId,
                paymentUrl    = paymentUrl,
                amount        = order.TotalAmount,
                provider      = dto.Provider
            };
        }

        public async Task<bool> ProcessCallbackAsync(PaymentCallbackDto dto)
        {
            // Tìm đơn hàng theo OrderCode hoặc TransactionId
            Order? order = null;
            if (!string.IsNullOrEmpty(dto.OrderCode))
            {
                order = await _context.Orders
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.OrderCode == dto.OrderCode);
            }

            Payment? payment = null;
            if (!string.IsNullOrEmpty(dto.TransactionId))
            {
                payment = await _context.Payments
                    .Include(p => p.Order)
                    .FirstOrDefaultAsync(p => p.TransactionId == dto.TransactionId);

                if (payment?.Order != null && order == null)
                    order = payment.Order;
            }

            if (order == null && payment == null) return false;

            if (order != null && payment == null)
            {
                payment = order.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
                if (payment == null)
                {
                    payment = new Payment
                    {
                        OrderId       = order.Id,
                        Amount        = order.TotalAmount,
                        Provider      = "vnpay",
                        TransactionId = "TXN" + Guid.NewGuid().ToString()[..8].ToUpper(),
                        Status        = "pending"
                    };
                    _context.Payments.Add(payment);
                }
            }

            var isSuccess = dto.Status.ToLowerInvariant() == "success";
            payment!.Status = isSuccess ? "success" : "failed";
            payment.PaidAt  = isSuccess ? DateTime.UtcNow : null;

            if (isSuccess && order != null)
            {
                order.PaymentStatus = "paid";
                if (order.Status == "pending")
                    order.Status = "processing";
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
