namespace Web.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "pending"; // pending | processing | shipped | delivered | cancelled
        public string ShippingAddress { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty; // cod | online
        public string PaymentStatus { get; set; } = "unpaid"; // unpaid | paid | refunded
        public DateTime OrderedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public List<OrderItem> OrderItems { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string? Variant { get; set; }
        public string? Color { get; set; }

        // Navigation
        public Order? Order { get; set; }
        public Product? Product { get; set; }
    }
}
