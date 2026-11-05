namespace Web.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Role { get; set; } = "customer"; // "customer" | "admin" | "warehouse"
        public bool IsLocked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public List<Order> Orders { get; set; } = new();
        public List<Cart> CartItems { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();
    }
}
