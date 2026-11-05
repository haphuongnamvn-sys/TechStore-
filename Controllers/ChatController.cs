using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private string GetCurrentUserName()
        {
            return User.FindFirst("FullName")?.Value
                ?? User.FindFirst("name")?.Value
                ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? "Nhân viên CSKH";
        }

        /// <summary>GET /api/chat/history?sessionId=...</summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return BadRequest(new { success = false, message = "SessionId không được để trống" });

            var userId = GetCurrentUserId();
            var history = await _chatService.GetHistoryAsync(sessionId, userId);
            return Ok(new { success = true, history });
        }

        /// <summary>POST /api/chat/send - Khách hàng gửi tin nhắn (Tới AI Bot hoặc Nhân viên CSKH)</summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { success = false, message = "Nội dung tin nhắn không được để trống" });

            var sessionId = !string.IsNullOrWhiteSpace(dto.SessionId) ? dto.SessionId : Guid.NewGuid().ToString("N");
            var userId = GetCurrentUserId();
            var customerName = User.FindFirst("FullName")?.Value ?? User.FindFirst("name")?.Value;

            // Lấy role của user hiện tại để phân luồng chatbot
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                        ?? User.FindFirst("role")?.Value;

            var result = await _chatService.SendCustomerMessageAsync(userId, sessionId, dto.Message, dto.Target ?? "bot", customerName, userRole);
            return Ok(new { success = true, sessionId, message = result });
        }

        /// <summary>POST /api/chat - Legacy backward compatible endpoint</summary>
        [HttpPost]
        public async Task<IActionResult> ChatLegacy([FromBody] ChatRequestDto dto)
        {
            var userId = GetCurrentUserId();
            var response = await _chatService.GetAiResponseWithContextAsync(dto.Message, userId);
            return Ok(new { success = true, reply = response });
        }

        // DÀNH CHO NHÂN VIÊN CHĂM SÓC KHÁCH HÀNG (ROLES: admin, support)

        /// <summary>GET /api/chat/staff/conversations</summary>
        [HttpGet("staff/conversations")]
        [Authorize(Roles = "admin,support")]
        public async Task<IActionResult> GetStaffConversations()
        {
            var conversations = await _chatService.GetConversationsAsync();
            return Ok(new { success = true, conversations });
        }

        /// <summary>GET /api/chat/staff/conversations/{sessionId}</summary>
        [HttpGet("staff/conversations/{sessionId}")]
        [Authorize(Roles = "admin,support")]
        public async Task<IActionResult> GetStaffConversationMessages(string sessionId)
        {
            var messages = await _chatService.GetConversationMessagesAsync(sessionId);
            return Ok(new { success = true, messages });
        }

        /// <summary>POST /api/chat/staff/reply - Nhân viên CSKH phản hồi tin nhắn khách hàng</summary>
        [HttpPost("staff/reply")]
        [Authorize(Roles = "admin,support")]
        public async Task<IActionResult> StaffReply([FromBody] StaffReplyDto dto)
        {
            var staffId = GetCurrentUserId() ?? 0;
            var staffName = GetCurrentUserName();

            var result = await _chatService.SendStaffReplyAsync(staffId, staffName, dto.SessionId, dto.Message);
            return Ok(new { success = true, message = result });
        }

        /// <summary>DELETE /api/chat/staff/conversations/{sessionId} - Nhân viên CSKH xóa cuộc trò chuyện</summary>
        [HttpDelete("staff/conversations/{sessionId}")]
        [Authorize(Roles = "admin,support")]
        public async Task<IActionResult> DeleteStaffConversation(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return BadRequest(new { success = false, message = "SessionId không được để trống" });

            var success = await _chatService.DeleteConversationAsync(sessionId);
            return Ok(new { success });
        }

        /// <summary>POST /api/chat/staff/conversations/{sessionId}/claim - Nhân viên tiếp nhận hỗ trợ khách hàng</summary>
        [HttpPost("staff/conversations/{sessionId}/claim")]
        [Authorize(Roles = "admin,support")]
        public async Task<IActionResult> ClaimConversation(string sessionId)
        {
            var staffId = GetCurrentUserId() ?? 0;
            var staffName = GetCurrentUserName();

            var (success, message) = await _chatService.ClaimConversationAsync(sessionId, staffId, staffName);
            if (!success) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message });
        }

        /// <summary>POST /api/chat/staff/conversations/{sessionId}/release - Chuyển giao về hàng đợi Chờ tiếp nhận</summary>
        [HttpPost("staff/conversations/{sessionId}/release")]
        [Authorize(Roles = "admin,support")]
        public async Task<IActionResult> ReleaseConversation(string sessionId)
        {
            var staffId = GetCurrentUserId() ?? 0;
            var (success, message) = await _chatService.ReleaseConversationAsync(sessionId, staffId);
            return Ok(new { success, message });
        }

        /// <summary>POST /api/chat/staff/conversations/{sessionId}/transfer - Chuyển giao khách hàng cho nhân viên khác (hoặc về hàng đợi)</summary>
        [HttpPost("staff/conversations/{sessionId}/transfer")]
        [Authorize(Roles = "admin,support")]
        public async Task<IActionResult> TransferConversation(string sessionId, [FromBody] TransferConversationDto dto)
        {
            var staffId = GetCurrentUserId() ?? 0;
            var staffName = GetCurrentUserName();

            var (success, message) = await _chatService.TransferConversationAsync(sessionId, staffId, staffName, dto?.TargetStaffId, dto?.Note);
            if (!success) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message });
        }

        /// <summary>GET /api/chat/staff/list - Lấy danh sách nhân viên CSKH để chuyển giao</summary>
        [HttpGet("staff/list")]
        [Authorize(Roles = "admin,support")]
        public async Task<IActionResult> GetStaffList()
        {
            var list = await _chatService.GetSupportStaffListAsync();
            return Ok(new { success = true, list });
        }

        /// <summary>POST /api/chat/merge-guest - Ghép phiên chat guest vào tài khoản khi khách đăng nhập</summary>
        [HttpPost("merge-guest")]
        [Authorize]
        public async Task<IActionResult> MergeGuestSession([FromBody] MergeGuestSessionDto dto)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue || string.IsNullOrWhiteSpace(dto?.GuestSessionId))
                return Ok(new { success = false });

            var customerName = User.FindFirst("FullName")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "Khách hàng";
            var success = await _chatService.MergeGuestSessionAsync(dto.GuestSessionId, userId.Value, customerName);
            return Ok(new { success });
        }

        /// <summary>POST /api/chat/staff/conversations/{sessionId}/close - Hoàn thành và đóng phiên hỗ trợ</summary>
        [HttpPost("staff/conversations/{sessionId}/close")]
        [Authorize(Roles = "admin,support")]
        public async Task<IActionResult> CloseConversation(string sessionId)
        {
            var staffId = GetCurrentUserId() ?? 0;
            var (success, message) = await _chatService.CloseConversationAsync(sessionId, staffId);
            return Ok(new { success, message });
        }
    }
}
