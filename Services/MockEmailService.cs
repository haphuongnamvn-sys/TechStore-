namespace Web.Services
{
    using Web.Services.Interfaces;

    public class MockEmailService : IEmailService
    {
        public Task SendOrderConfirmationEmailAsync(string toEmail, string orderCode, decimal totalAmount)
        {
            // Simulate sending an email
            Console.WriteLine("\n=======================================================");
            Console.WriteLine($"[EMAIL SENT TO]: {toEmail}");
            Console.WriteLine($"[SUBJECT]: Xác nhận đơn hàng #{orderCode}");
            Console.WriteLine($"[BODY]: Cảm ơn bạn đã đặt hàng. Tổng tiền của bạn là {totalAmount:N0} VNĐ.");
            Console.WriteLine("=======================================================\n");

            return Task.CompletedTask;
        }
    }
}
