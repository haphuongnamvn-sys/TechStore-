namespace Web.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendOrderConfirmationEmailAsync(string toEmail, string orderCode, decimal totalAmount);
    }
}
