namespace Web.Models
{
    public class Brand
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;

        // Navigation
        public List<Product> Products { get; set; } = new();
    }
}
