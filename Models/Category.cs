namespace Web.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string SpecTemplatesJson { get; set; } = string.Empty;

        // Navigation
        public Category? Parent { get; set; }
        public List<Category> Children { get; set; } = new();
        public List<Product> Products { get; set; } = new();
    }
}
