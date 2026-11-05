namespace Web.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Provider { get; set; } = string.Empty; // vnpay | momo | stripe
        public string Status { get; set; } = "pending"; // pending | success | failed
        public DateTime? PaidAt { get; set; }

        // Navigation
        public Order? Order { get; set; }
    }
}
