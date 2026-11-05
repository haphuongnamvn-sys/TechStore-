namespace Web.Models
{
    public class Review
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public int? OrderId { get; set; }
        public int Rating { get; set; } // 1-5
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public Product? Product { get; set; }
        public Order? Order { get; set; }
    }

    public class ReviewMediaItem
    {
        public string Url { get; set; } = string.Empty;
        public string Type { get; set; } = "image"; // "image" or "video"
        public string? FileName { get; set; }
    }
}
