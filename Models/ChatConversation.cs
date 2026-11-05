using System.ComponentModel.DataAnnotations;

namespace Web.Models
{
    public class ChatConversation
    {
        [Key]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        public int? CustomerUserId { get; set; }

        [MaxLength(100)]
        public string CustomerName { get; set; } = "Khách hàng";

        [MaxLength(50)]
        public string CustomerPhone { get; set; } = string.Empty;

        // Nhân viên CSKH đang phụ trách (tránh 2-3 nhân viên trả lời trùng người)
        public int? AssignedStaffId { get; set; }

        [MaxLength(100)]
        public string? AssignedStaffName { get; set; }

        // Trạng thái: "waiting" (Chờ tiếp nhận) | "in_progress" (Đang hỗ trợ) | "closed" (Đã kết thúc)
        [MaxLength(20)]
        public string Status { get; set; } = "waiting";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AssignedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
    }
}
