namespace Web.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal SalePrice { get; set; }
        public int CategoryId { get; set; }
        public int BrandId { get; set; }
        public int StockQuantity { get; set; }
        public double RatingAvg { get; set; }
        public string Badge { get; set; } = string.Empty; // "new" | "hot" | "sale"
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Category? Category { get; set; }
        public Brand? Brand { get; set; }
        public List<ProductImage> Images { get; set; } = new();
        public List<ProductSpec> Specs { get; set; } = new();
        public List<ProductHighlight> Highlights { get; set; } = new();
        public List<ProductVariant> Variants { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();
        public List<Cart> CartItems { get; set; } = new();
        public List<OrderItem> OrderItems { get; set; } = new();
    }
}
