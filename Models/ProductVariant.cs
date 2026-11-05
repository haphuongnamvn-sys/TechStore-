namespace Web.Models
{
    public class ProductVariant
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string VariantName { get; set; } = string.Empty; // e.g. "128GB", "256GB", "512GB", "1TB"
        public string ColorName { get; set; } = string.Empty;   // e.g. "Titan Tự Nhiên", "Titan Đen"
        public string? ColorCode { get; set; }                  // e.g. "#b3aca4", "#3b3c3e"
        public decimal Price { get; set; }                      // Giá bán của biến thể
        public decimal? OriginalPrice { get; set; }             // Giá niêm yết gốc
        public int StockQuantity { get; set; }                  // Số lượng tồn kho riêng
        public string? Sku { get; set; }                        // Mã SKU
        public string? ImageUrl { get; set; }                   // Ảnh đại diện riêng cho biến thể nếu có

        // Navigation
        public Product? Product { get; set; }
    }
}
