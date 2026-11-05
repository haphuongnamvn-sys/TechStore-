using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>POST /api/payments - Tạo giao dịch thanh toán online</summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentDto dto)
        {
            var result = await _paymentService.CreatePaymentAsync(dto);
            return Ok(result);
        }

        /// <summary>POST /api/payments/callback - Nhận callback từ Payment Gateway (Hỗ trợ cả body lẫn query params)</summary>
        [HttpPost("callback")]
        public async Task<IActionResult> Callback(
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] PaymentCallbackDto? bodyDto,
            [FromQuery] string? orderCode,
            [FromQuery] string? status,
            [FromQuery] string? transactionId)
        {
            var dto = bodyDto ?? new PaymentCallbackDto();
            if (!string.IsNullOrEmpty(orderCode)) dto.OrderCode = orderCode;
            if (!string.IsNullOrEmpty(status)) dto.Status = status;
            if (!string.IsNullOrEmpty(transactionId)) dto.TransactionId = transactionId;

            var success = await _paymentService.ProcessCallbackAsync(dto);
            if (!success)
                return BadRequest(new { success = false, message = "Không tìm thấy giao dịch hoặc đơn hàng tương ứng" });

            return Ok(new { success = true, message = "Cập nhật trạng thái thanh toán thành công" });
        }
    }
}
