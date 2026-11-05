namespace Web.Models
{
    public class Cart
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string? Variant { get; set; }
        public string? Color { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public Product? Product { get; set; }
    }
}
