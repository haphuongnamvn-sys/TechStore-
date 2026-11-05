using Web.Models;

namespace Web.Services.Interfaces
{
    public interface IChatService
    {
        Task<List<ChatMessageDto>> GetHistoryAsync(string sessionId, int? userId);
        Task<ChatMessageDto> SendCustomerMessageAsync(int? userId, string sessionId, string message, string target, string? customerName, string? userRole = null);
        Task<List<ConversationSummaryDto>> GetConversationsAsync();
        Task<List<ChatMessageDto>> GetConversationMessagesAsync(string sessionId);
        Task<ChatMessageDto> SendStaffReplyAsync(int staffUserId, string staffName, string sessionId, string message);
        Task<string> GetAiResponseWithContextAsync(string message, int? userId);
        Task<bool> DeleteConversationAsync(string sessionId);
        Task<(bool success, string message)> ClaimConversationAsync(string sessionId, int staffId, string staffName);
        Task<(bool success, string message)> ReleaseConversationAsync(string sessionId, int staffId);
        Task<(bool success, string message)> TransferConversationAsync(string sessionId, int fromStaffId, string fromStaffName, int? targetStaffId, string? note);
        Task<(bool success, string message)> CloseConversationAsync(string sessionId, int staffId);
        Task<bool> MergeGuestSessionAsync(string guestSessionId, int userId, string customerName);
        Task<List<SupportStaffDto>> GetSupportStaffListAsync();
    }
}
