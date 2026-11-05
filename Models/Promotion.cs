namespace Web.Models
{
    public class Promotion
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DiscountType { get; set; } = "percent"; // percent | fixed
        public decimal DiscountValue { get; set; }
        public decimal MinOrderValue { get; set; }
        public int MaxUsage { get; set; }
        public int UsedCount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
