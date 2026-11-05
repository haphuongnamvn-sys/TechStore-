namespace Web.Models
{
    public class ProductImage
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public int SortOrder { get; set; }

        // Navigation
        public Product? Product { get; set; }
    }

    public class ProductSpec
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string SpecKey { get; set; } = string.Empty;
        public string SpecValue { get; set; } = string.Empty;
        public int SortOrder { get; set; }

        // Navigation
        public Product? Product { get; set; }
    }

    public class ProductHighlight
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int SortOrder { get; set; }

        // Navigation
        public Product? Product { get; set; }
    }
}
