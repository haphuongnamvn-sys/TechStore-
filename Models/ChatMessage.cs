using System.ComponentModel.DataAnnotations;

namespace Web.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        public int? UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Sender { get; set; } = "customer"; // "customer" | "staff" | "bot"

        [MaxLength(100)]
        public string SenderName { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public bool IsStaffReply { get; set; } = false;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}
