using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Models;
using Web.Services.Interfaces;

namespace Web.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public ChatService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        }

        public async Task<List<ChatMessageDto>> GetHistoryAsync(string sessionId, int? userId)
        {
            // Nếu người dùng đã đăng nhập:
            // - Khách hàng (customer): Lịch sử gắn với user_{userId}
            // - Actor nội bộ (admin, warehouse, support): Lịch sử gắn với internal_{userId}
            var user = (userId.HasValue && userId.Value > 0) ? await _context.Users.FindAsync(userId.Value) : null;
            var effectiveSessionId = (user != null)
                ? (user.Role == "customer" ? $"user_{user.Id}" : $"internal_{user.Id}")
                : sessionId;

            var query = _context.ChatMessages.AsNoTracking().Where(m => m.SessionId == effectiveSessionId);

            var messages = await query
                .OrderBy(m => m.CreatedAt)
                .Take(100)
                .ToListAsync();

            var userIds = messages.Where(m => m.UserId.HasValue && m.UserId.Value > 0).Select(m => m.UserId!.Value).Distinct().ToList();
            var userMap = userIds.Any()
                ? await _context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName)
                : new Dictionary<int, string>();

            var list = messages.Select(m => new ChatMessageDto
            {
                Id           = m.Id,
                SessionId    = m.SessionId,
                Sender       = m.Sender,
                SenderName   = (m.UserId.HasValue && userMap.TryGetValue(m.UserId.Value, out var realName) && !string.IsNullOrWhiteSpace(realName))
                                ? realName
                                : m.SenderName,
                Message      = m.Message,
                IsStaffReply = m.IsStaffReply,
                CreatedAt    = m.CreatedAt
            }).ToList();

            return list;
        }

        public async Task<ChatMessageDto> SendCustomerMessageAsync(int? userId, string sessionId, string message, string target, string? customerName, string? userRole = null)
        {
            var user = (userId.HasValue && userId.Value > 0) ? await _context.Users.FindAsync(userId.Value) : null;
            var effectiveRole = userRole ?? user?.Role ?? "customer";
            var name = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName
                     : (!string.IsNullOrWhiteSpace(customerName) ? customerName : (userId.HasValue ? $"Khách #{userId.Value}" : "Khách hàng"));

            // Các actor không phải customer (admin, support, warehouse):
            // → Chat với bot nội bộ theo role, KHÔNG tạo ChatConversation,
            //   KHÔNG xuất hiện trong hàng đợi CSKH Desk.
            bool isInternalUser = userId.HasValue && userId.Value > 0
                                  && effectiveRole != "customer";

            if (isInternalUser)
            {
                // Session riêng biệt cho nội bộ: internal_{userId}
                var internalSessionId = $"internal_{userId!.Value}";
                var botReplyText = await GetInternalBotResponseAsync(message, effectiveRole, userId);

                var userMsg = new ChatMessage
                {
                    UserId       = userId,
                    SessionId    = internalSessionId,
                    Sender       = "customer",
                    SenderName   = name,
                    Message      = message.Trim(),
                    IsStaffReply = false,
                    IsRead       = true,
                    CreatedAt    = DateTime.UtcNow
                };
                _context.ChatMessages.Add(userMsg);

                var botMsg = new ChatMessage
                {
                    UserId       = null,
                    SessionId    = internalSessionId,
                    Sender       = "bot",
                    SenderName   = GetInternalBotName(effectiveRole),
                    Message      = botReplyText,
                    IsStaffReply = false,
                    IsRead       = true,
                    CreatedAt    = DateTime.UtcNow.AddMilliseconds(1)
                };
                _context.ChatMessages.Add(botMsg);
                await _context.SaveChangesAsync();

                return new ChatMessageDto
                {
                    Id           = botMsg.Id,
                    SessionId    = internalSessionId,
                    Sender       = botMsg.Sender,
                    SenderName   = botMsg.SenderName,
                    Message      = botMsg.Message,
                    IsStaffReply = false,
                    CreatedAt    = botMsg.CreatedAt
                };
            }

            // Khách hàng (customer) hoặc guest (chưa đăng nhập)

            // Phiên chat được định danh theo tài khoản nếu đã đăng nhập
            var effectiveSessionId = (userId.HasValue && userId.Value > 0)
                ? $"user_{userId.Value}"
                : sessionId;

            // Cập nhật hoặc khởi tạo thông tin khách hàng trên bảng ChatConversations
            var conv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.SessionId == effectiveSessionId);
            if (conv == null)
            {
                conv = new ChatConversation
                {
                    SessionId = effectiveSessionId,
                    CustomerUserId = userId,
                    CustomerName = name,
                    CustomerPhone = user?.Phone ?? string.Empty,
                    Status = "waiting",
                    CreatedAt = DateTime.UtcNow
                };
                _context.ChatConversations.Add(conv);
            }
            else
            {
                conv.CustomerName = name;
                if (!string.IsNullOrWhiteSpace(user?.Phone)) conv.CustomerPhone = user.Phone;
                // Nếu khách nhắn tiếp vào cuộc trò chuyện đã đóng, mở lại ở trạng thái chờ
                if (conv.Status == "closed")
                {
                    conv.Status = "waiting";
                }
            }

            // 1. Lưu tin nhắn của khách vào Database
            var customerMsg = new ChatMessage
            {
                UserId       = userId,
                SessionId    = effectiveSessionId,
                Sender       = "customer",
                SenderName   = name,
                Message      = message.Trim(),
                IsStaffReply = false,
                IsRead       = false,
                CreatedAt    = DateTime.UtcNow
            };
            _context.ChatMessages.Add(customerMsg);
            await _context.SaveChangesAsync();

            // 2. Nếu người dùng chọn chat với AI Bot
            if (target == "bot")
            {
                var botReplyText = await GetAiResponseWithContextAsync(message, userId);

                var botMsg = new ChatMessage
                {
                    UserId       = userId,
                    SessionId    = effectiveSessionId,
                    Sender       = "bot",
                    SenderName   = "Trợ lý AI TechStore",
                    Message      = botReplyText,
                    IsStaffReply = false,
                    IsRead       = true,
                    CreatedAt    = DateTime.UtcNow
                };
                _context.ChatMessages.Add(botMsg);
                await _context.SaveChangesAsync();

                return new ChatMessageDto
                {
                    Id           = botMsg.Id,
                    SessionId    = effectiveSessionId,
                    Sender       = botMsg.Sender,
                    SenderName   = botMsg.SenderName,
                    Message      = botMsg.Message,
                    IsStaffReply = false,
                    CreatedAt    = botMsg.CreatedAt
                };
            }

            // Nếu người dùng chọn chat với Nhân viên CSKH
            return new ChatMessageDto
            {
                Id           = customerMsg.Id,
                SessionId    = effectiveSessionId,
                Sender       = customerMsg.Sender,
                SenderName   = customerMsg.SenderName,
                Message      = customerMsg.Message,
                IsStaffReply = false,
                CreatedAt    = customerMsg.CreatedAt
            };
        }

        public async Task<List<ConversationSummaryDto>> GetConversationsAsync()
        {
            // Lấy danh sách các session duy nhất từ bảng ChatMessages
            // Chỉ lấy session thuộc customer hoặc guest (không có userId = nội bộ)
            // Lọc bỏ session internal_* (của admin/warehouse/support dùng bot nội bộ)
            var sessionGroups = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => !m.SessionId.StartsWith("internal_"))
                .GroupBy(m => m.SessionId)
                .Select(g => new
                {
                    SessionId     = g.Key,
                    UserId        = g.Max(m => m.UserId),
                    CustomerName  = g.Where(m => m.Sender == "customer").Select(m => m.SenderName).FirstOrDefault() ?? "Khách hàng",
                    LastMessageAt = g.Max(m => m.CreatedAt),
                    TotalMessages = g.Count(),
                    UnreadCount   = g.Count(m => m.Sender == "customer" && !m.IsRead)
                })
                .OrderByDescending(s => s.LastMessageAt)
                .Take(50)
                .ToListAsync();

            var result = new List<ConversationSummaryDto>();

            foreach (var s in sessionGroups)
            {
                var conv = await _context.ChatConversations.AsNoTracking().FirstOrDefaultAsync(c => c.SessionId == s.SessionId);
                var lastMsg = await _context.ChatMessages
                    .AsNoTracking()
                    .Where(m => m.SessionId == s.SessionId)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();

                var customerUserId = conv?.CustomerUserId ?? s.UserId;
                if (!customerUserId.HasValue && s.SessionId.StartsWith("user_") && int.TryParse(s.SessionId.Substring(5), out var parsedId))
                {
                    customerUserId = parsedId;
                }

                var custUser = (customerUserId.HasValue && customerUserId.Value > 0)
                    ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerUserId.Value)
                    : null;

                // Bỏ qua session thuộc user nội bộ (admin, warehouse, support)
                // Session guest (custUser == null) thì vẫn hiển thị
                if (custUser != null && custUser.Role != "customer")
                    continue;

                var customerName = !string.IsNullOrWhiteSpace(custUser?.FullName)
                    ? custUser.FullName
                    : (conv?.CustomerName ?? s.CustomerName);

                var phone = custUser?.Phone ?? conv?.CustomerPhone ?? string.Empty;

                var staffObj = (conv?.AssignedStaffId.HasValue == true && conv.AssignedStaffId.Value > 0)
                    ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == conv.AssignedStaffId.Value)
                    : null;
                var assignedStaffName = staffObj?.FullName ?? conv?.AssignedStaffName;

                result.Add(new ConversationSummaryDto
                {
                    SessionId         = s.SessionId,
                    UserId            = customerUserId,
                    CustomerName      = customerName,
                    CustomerPhone     = phone,
                    LastMessage       = lastMsg?.Message ?? string.Empty,
                    LastSender        = lastMsg?.Sender ?? "customer",
                    LastMessageAt     = s.LastMessageAt,
                    UnreadCount       = s.UnreadCount,
                    TotalMessages     = s.TotalMessages,
                    AssignedStaffId   = conv?.AssignedStaffId,
                    AssignedStaffName = assignedStaffName,
                    Status            = conv?.Status ?? "waiting"
                });
            }

            return result;
        }

        public async Task<List<ChatMessageDto>> GetConversationMessagesAsync(string sessionId)
        {
            // Đánh dấu đã đọc các tin nhắn từ khách
            var unread = await _context.ChatMessages
                .Where(m => m.SessionId == sessionId && m.Sender == "customer" && !m.IsRead)
                .ToListAsync();

            if (unread.Any())
            {
                foreach (var m in unread) m.IsRead = true;
                await _context.SaveChangesAsync();
            }

            var messages = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            var userIds = messages.Where(m => m.UserId.HasValue && m.UserId.Value > 0).Select(m => m.UserId!.Value).Distinct().ToList();
            var userMap = userIds.Any()
                ? await _context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName)
                : new Dictionary<int, string>();

            var list = messages.Select(m => new ChatMessageDto
            {
                Id           = m.Id,
                SessionId    = m.SessionId,
                Sender       = m.Sender,
                SenderName   = (m.UserId.HasValue && userMap.TryGetValue(m.UserId.Value, out var realName) && !string.IsNullOrWhiteSpace(realName))
                                ? realName
                                : m.SenderName,
                Message      = m.Message,
                IsStaffReply = m.IsStaffReply,
                CreatedAt    = m.CreatedAt
            }).ToList();

            return list;
        }

        public async Task<ChatMessageDto> SendStaffReplyAsync(int staffUserId, string staffName, string sessionId, string message)
        {
            var staffUser = (staffUserId > 0) ? await _context.Users.FindAsync(staffUserId) : null;
            var effectiveStaffName = !string.IsNullOrWhiteSpace(staffUser?.FullName)
                ? staffUser.FullName
                : (string.IsNullOrWhiteSpace(staffName) ? "Nhân viên CSKH" : staffName);

            var msg = new ChatMessage
            {
                UserId       = staffUserId,
                SessionId    = sessionId,
                Sender       = "staff",
                SenderName   = effectiveStaffName,
                Message      = message.Trim(),
                IsStaffReply = true,
                IsRead       = true,
                CreatedAt    = DateTime.UtcNow
            };

            _context.ChatMessages.Add(msg);

            // Cập nhật trạng thái hội thoại sang Đang hỗ trợ nếu chưa gán
            var conv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.SessionId == sessionId);
            if (conv != null)
            {
                if (!conv.AssignedStaffId.HasValue)
                {
                    conv.AssignedStaffId = staffUserId;
                    conv.AssignedStaffName = effectiveStaffName;
                    conv.Status = "in_progress";
                    conv.AssignedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            return new ChatMessageDto
            {
                Id           = msg.Id,
                SessionId    = msg.SessionId,
                Sender       = msg.Sender,
                SenderName   = msg.SenderName,
                Message      = msg.Message,
                IsStaffReply = true,
                CreatedAt    = msg.CreatedAt
            };
        }

        public async Task<(bool success, string message)> ClaimConversationAsync(string sessionId, int staffId, string staffName)
        {
            var conv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.SessionId == sessionId);
            var staffUser = (staffId > 0) ? await _context.Users.FindAsync(staffId) : null;
            var effectiveStaffName = !string.IsNullOrWhiteSpace(staffUser?.FullName)
                ? staffUser.FullName
                : (string.IsNullOrWhiteSpace(staffName) ? "Nhân viên CSKH" : staffName);

            if (conv == null)
            {
                var firstCustMsg = await _context.ChatMessages.FirstOrDefaultAsync(m => m.SessionId == sessionId && m.Sender == "customer");
                conv = new ChatConversation
                {
                    SessionId = sessionId,
                    CustomerName = firstCustMsg?.SenderName ?? "Khách hàng",
                    Status = "waiting",
                    CreatedAt = DateTime.UtcNow
                };
                _context.ChatConversations.Add(conv);
            }

            // Kiểm tra xem đã có ai khác tiếp nhận chưa:
            if (conv.AssignedStaffId.HasValue && conv.AssignedStaffId.Value != staffId && conv.Status == "in_progress")
            {
                return (false, $"Hội thoại này đang được nhân viên {conv.AssignedStaffName} phụ trách rồi!");
            }

            conv.AssignedStaffId = staffId;
            conv.AssignedStaffName = effectiveStaffName;
            conv.Status = "in_progress";
            conv.AssignedAt = DateTime.UtcNow;

            // Thêm 1 tin nhắn tự động từ nhân viên vừa tiếp nhận
            var sysMsg = new ChatMessage
            {
                SessionId    = sessionId,
                UserId       = staffId,
                Sender       = "staff",
                SenderName   = effectiveStaffName,
                Message      = $"Dạ chào anh/chị! Em là {effectiveStaffName} (CSKH TechStore), em sẽ trực tiếp hỗ trợ giải đáp cho anh/chị ngay đây ạ! 🎧",
                IsStaffReply = true,
                IsRead       = true,
                CreatedAt    = DateTime.UtcNow
            };
            _context.ChatMessages.Add(sysMsg);
            await _context.SaveChangesAsync();

            return (true, "Đã tiếp nhận hỗ trợ khách hàng thành công!");
        }

        public async Task<(bool success, string message)> ReleaseConversationAsync(string sessionId, int staffId)
        {
            var conv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.SessionId == sessionId);
            if (conv == null) return (false, "Không tìm thấy khách hàng");

            conv.AssignedStaffId = null;
            conv.AssignedStaffName = null;
            conv.Status = "waiting";

            var releaseNotice = "🔄 Cuộc trò chuyện đã được chuyển giao về hàng đợi Chờ tiếp nhận để nhân viên khác tiếp quản.";
            var sysMsg = new ChatMessage
            {
                SessionId    = sessionId,
                UserId       = staffId,
                Sender       = "staff",
                SenderName   = "Hệ thống CSKH",
                Message      = releaseNotice,
                IsStaffReply = true,
                IsRead       = true,
                CreatedAt    = DateTime.UtcNow
            };
            _context.ChatMessages.Add(sysMsg);
            await _context.SaveChangesAsync();

            return (true, "Đã chuyển giao khách hàng về trạng thái Chờ tiếp nhận");
        }

        public async Task<(bool success, string message)> TransferConversationAsync(string sessionId, int fromStaffId, string fromStaffName, int? targetStaffId, string? note)
        {
            var conv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.SessionId == sessionId);
            var fromUser = (fromStaffId > 0) ? await _context.Users.FindAsync(fromStaffId) : null;
            var effectiveFromName = !string.IsNullOrWhiteSpace(fromUser?.FullName)
                ? fromUser.FullName
                : (string.IsNullOrWhiteSpace(fromStaffName) ? "CSKH" : fromStaffName);

            if (conv == null)
            {
                var firstCustMsg = await _context.ChatMessages.FirstOrDefaultAsync(m => m.SessionId == sessionId && m.Sender == "customer");
                conv = new ChatConversation
                {
                    SessionId = sessionId,
                    CustomerName = firstCustMsg?.SenderName ?? "Khách hàng",
                    Status = "waiting",
                    CreatedAt = DateTime.UtcNow
                };
                _context.ChatConversations.Add(conv);
            }

            string transferNotice;
            if (targetStaffId.HasValue && targetStaffId.Value > 0)
            {
                var targetUser = await _context.Users.FindAsync(targetStaffId.Value);
                if (targetUser == null) return (false, "Không tìm thấy nhân viên nhận chuyển giao");

                conv.AssignedStaffId = targetUser.Id;
                conv.AssignedStaffName = targetUser.FullName;
                conv.Status = "in_progress";
                conv.AssignedAt = DateTime.UtcNow;

                transferNotice = $"🔄 Nhân viên {effectiveFromName} đã chuyển giao khách hàng cho {targetUser.FullName} tiếp tục hỗ trợ." +
                    (!string.IsNullOrWhiteSpace(note) ? $" (Ghi chú: {note.Trim()})" : "");
            }
            else
            {
                conv.AssignedStaffId = null;
                conv.AssignedStaffName = null;
                conv.Status = "waiting";

                transferNotice = $"🔄 Nhân viên {effectiveFromName} đã chuyển giao khách hàng về hàng đợi Chờ tiếp nhận để nhân viên khác tiếp quản." +
                    (!string.IsNullOrWhiteSpace(note) ? $" (Ghi chú: {note.Trim()})" : "");
            }

            // Ghi nhận mốc chuyển giao vào lịch sử chat (không xóa bất kỳ tin nhắn cũ nào)
            var sysMsg = new ChatMessage
            {
                SessionId    = sessionId,
                UserId       = fromStaffId,
                Sender       = "staff",
                SenderName   = effectiveFromName,
                Message      = transferNotice,
                IsStaffReply = true,
                IsRead       = true,
                CreatedAt    = DateTime.UtcNow
            };
            _context.ChatMessages.Add(sysMsg);
            await _context.SaveChangesAsync();

            return (true, "Đã chuyển giao hỗ trợ khách hàng thành công!");
        }

        public async Task<bool> MergeGuestSessionAsync(string guestSessionId, int userId, string customerName)
        {
            if (string.IsNullOrWhiteSpace(guestSessionId)) return false;
            var targetSessionId = $"user_{userId}";
            if (guestSessionId == targetSessionId) return true;

            var guestMsgs = await _context.ChatMessages
                .Where(m => m.SessionId == guestSessionId)
                .ToListAsync();

            if (guestMsgs.Any())
            {
                foreach (var m in guestMsgs)
                {
                    m.SessionId = targetSessionId;
                    if (m.Sender == "customer")
                    {
                        m.UserId = userId;
                        m.SenderName = customerName;
                    }
                }
            }

            var guestConv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.SessionId == guestSessionId);
            var userConv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.SessionId == targetSessionId);

            if (guestConv != null)
            {
                if (userConv == null)
                {
                    guestConv.SessionId = targetSessionId;
                    guestConv.CustomerUserId = userId;
                    guestConv.CustomerName = customerName;
                }
                else
                {
                    _context.ChatConversations.Remove(guestConv);
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<SupportStaffDto>> GetSupportStaffListAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => (u.Role == "support" || u.Role == "admin") && !u.IsLocked)
                .Select(u => new SupportStaffDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    Role = u.Role
                })
                .ToListAsync();
        }

        public async Task<(bool success, string message)> CloseConversationAsync(string sessionId, int staffId)
        {
            var conv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.SessionId == sessionId);
            if (conv == null) return (false, "Không tìm thấy hội thoại");

            conv.Status = "closed";
            conv.ClosedAt = DateTime.UtcNow;

            var closeMsg = new ChatMessage
            {
                SessionId    = sessionId,
                UserId       = staffId,
                Sender       = "staff",
                SenderName   = "CSKH TechStore",
                Message      = "Phiên hỗ trợ đã hoàn thành. TechStore cảm ơn quý khách đã liên hệ! Chúc quý khách một ngày tốt lành! ⭐",
                IsStaffReply = true,
                IsRead       = true,
                CreatedAt    = DateTime.UtcNow
            };
            _context.ChatMessages.Add(closeMsg);
            await _context.SaveChangesAsync();

            return (true, "Đã hoàn thành và đóng phiên hỗ trợ");
        }

        public async Task<bool> DeleteConversationAsync(string sessionId)
        {
            var msgs = await _context.ChatMessages.Where(m => m.SessionId == sessionId).ToListAsync();
            if (msgs.Any())
            {
                _context.ChatMessages.RemoveRange(msgs);
            }

            var conv = await _context.ChatConversations.FirstOrDefaultAsync(c => c.SessionId == sessionId);
            if (conv != null)
            {
                _context.ChatConversations.Remove(conv);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Lấy tên bot hiển thị theo role của người dùng nội bộ
        /// </summary>
        private static string GetInternalBotName(string role) => role switch
        {
            "admin"     => "🛠️ Trợ lý Quản trị TechStore",
            "warehouse" => "📦 Trợ lý Kho hàng TechStore",
            "support"   => "🎧 Hệ thống CSKH TechStore",
            _           => "🤖 Trợ lý TechStore"
        };

        /// <summary>
        /// Bot nội bộ theo role: admin, warehouse, support
        /// Tích hợp Trí tuệ Nhân tạo AI (Gemini + Ngữ cảnh CSDL nội bộ)
        /// Trả lời phù hợp quyền hạn, CHỈ CÓ AI TRẢ LỜI, KHÔNG ĐẨY VÀO HÀNG ĐỢI CSKH
        /// </summary>
        private async Task<string> GetInternalBotResponseAsync(string message, string role, int? userId)
        {
            var lower = message.ToLower().Trim();
            var apiKey = _configuration["GeminiApiKey"];

            if (role == "admin")
            {
                // Thu thập số liệu CSDL thời gian thực cho Admin
                var totalOrders = await _context.Orders.CountAsync();
                var pendingOrders = await _context.Orders.CountAsync(o => o.Status == "pending");
                var todayOrders = await _context.Orders.CountAsync(o => o.OrderedAt.Date == DateTime.UtcNow.Date);
                var totalUsers = await _context.Users.CountAsync(u => u.Role == "customer");
                var customers = totalUsers;
                var staff = await _context.Users.CountAsync(u => u.Role == "support" || u.Role == "warehouse" || u.Role == "admin");
                var totalProducts = await _context.Products.CountAsync();
                var outOfStock = await _context.Products.CountAsync(p => p.StockQuantity == 0);
                var totalPaidRevenue = await _context.Orders
                    .Where(o => o.Status != "cancelled" && o.PaymentStatus == "paid")
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

                var adminSystemInstruction = 
                    "Bạn là Trợ lý AI Quản trị viên (Admin Assistant) nội bộ của hệ thống TechStore.\n" +
                    "Bạn CHỈ hỗ trợ Quản trị viên TechStore và hoàn toàn độc lập với khách hàng.\n" +
                    "DỮ LIỆU CSDL HỆ THỐNG THỜI GIAN THỰC:\n" +
                    $"- Doanh thu: Đã xác nhận thanh toán {totalPaidRevenue:N0} VNĐ.\n" +
                    $"- Đơn hàng: Tổng {totalOrders} đơn, Chờ duyệt/xác nhận {pendingOrders} đơn, Đơn hôm nay {todayOrders} đơn.\n" +
                    $"- Khách hàng: {customers} tài khoản khách hàng, {staff} nhân sự nội bộ (CSKH, Kho, Admin).\n" +
                    $"- Sản phẩm & Tồn kho: Tổng {totalProducts} sản phẩm, {outOfStock} sản phẩm đang hết hàng (tồn = 0).\n" +
                    "QUY TẮC TRẢ LỜI:\n" +
                    "1. Trả lời bằng tiếng Việt, súc tích, chuyên nghiệp, dùng biểu tượng cảm xúc (emoji) và định dạng markdown rõ ràng.\n" +
                    "2. Sử dụng chính xác các số liệu thống kê được cung cấp ở trên khi admin hỏi về đơn hàng, tài khoản, hàng hóa.\n" +
                    "3. Hướng dẫn admin vào các mục tương ứng trong menu Admin (Quản lý đơn hàng, Quản lý sản phẩm, Quản lý người dùng, Báo cáo & Thống kê) khi cần thao tác chi tiết.";

                if (!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_API_KEY_HERE")
                {
                    try
                    {
                        var geminiReply = await CallGeminiWithSystemInstructionAsync(apiKey, adminSystemInstruction, message);
                        if (!string.IsNullOrWhiteSpace(geminiReply)) return geminiReply;
                    }
                    catch { }
                }

                // Fallback nếu không có kết nối internet hoặc API timeout
                if (lower.Contains("đơn hàng") || lower.Contains("order"))
                {
                    return $"📊 **Thống kê đơn hàng hiện tại:**\n" +
                           $"- Tổng đơn: **{totalOrders}**\n" +
                           $"- Chờ xác nhận: **{pendingOrders}**\n" +
                           $"- Hôm nay: **{todayOrders}**\n\n" +
                           $"👉 Vào **Admin → Quản lý đơn hàng** để xem chi tiết và xử lý từng đơn.";
                }
                if (lower.Contains("người dùng") || lower.Contains("tài khoản") || lower.Contains("user"))
                {
                    return $"👥 **Thống kê tài khoản:**\n" +
                           $"- Tổng người dùng: **{totalUsers}**\n" +
                           $"- Khách hàng: **{customers}**\n" +
                           $"- Nhân viên CSKH: **{staff}**\n\n" +
                           $"👉 Vào **Admin → Quản lý người dùng** để thêm/sửa/khóa tài khoản.";
                }
                if (lower.Contains("sản phẩm") || lower.Contains("hàng hóa"))
                {
                    return $"🛍️ **Thống kê sản phẩm:**\n" +
                           $"- Tổng sản phẩm: **{totalProducts}**\n" +
                           $"- Hết hàng: **{outOfStock}**\n\n" +
                           $"👉 Vào **Admin → Quản lý sản phẩm** để thêm/sửa/xóa sản phẩm.";
                }
                if (lower.Contains("doanh thu") || lower.Contains("báo cáo") || lower.Contains("thống kê"))
                {
                    return $"📈 **Báo cáo doanh thu:** Vui lòng vào **Admin → Báo cáo & Thống kê** để xem biểu đồ doanh thu chi tiết theo ngày/tháng/năm.\n\n" +
                           $"💡 Tôi có thể giúp bạn tra cứu nhanh: đơn hàng, người dùng, sản phẩm, kho hàng. Hãy hỏi thêm nhé!";
                }
                return $"👋 Xin chào **Admin**! Tôi là Trợ lý AI Quản trị nội bộ TechStore.\n\n" +
                       $"Tôi có thể hỗ trợ bạn:\n" +
                       $"- 📊 Thống kê **đơn hàng** (hôm nay: {todayOrders}, chờ xử lý: {pendingOrders})\n" +
                       $"- 👥 Tra cứu **người dùng & tài khoản** ({customers} khách hàng)\n" +
                       $"- 🛍️ Kiểm tra **sản phẩm & tồn kho** ({outOfStock} sản phẩm hết hàng)\n" +
                       $"- 📈 Hướng dẫn xem **báo cáo doanh thu**\n\n" +
                       $"Hãy hỏi tôi điều bạn cần!";
            }

            if (role == "warehouse")
            {
                // Thu thập số liệu tồn kho cho Thủ kho
                var lowStockProducts = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.StockQuantity <= 10)
                    .OrderBy(p => p.StockQuantity)
                    .Take(8)
                    .Select(p => new { p.Name, p.StockQuantity })
                    .ToListAsync();
                var totalProducts = await _context.Products.CountAsync();

                var warehouseInstruction = 
                    "Bạn là Trợ lý AI Quản lý Kho hàng (Warehouse Assistant) nội bộ của TechStore.\n" +
                    "Bạn CHỈ hỗ trợ Thủ kho TechStore theo dõi kho, nhập xuất và tồn kho hàng hóa.\n" +
                    "DỮ LIỆU KHO HÀNG THỜI GIAN THỰC:\n" +
                    $"- Tổng số mã sản phẩm trong kho: {totalProducts}\n" +
                    $"- Sản phẩm sắp hết hàng (tồn <= 10): {string.Join("; ", lowStockProducts.Select(p => $"{p.Name} (còn {p.StockQuantity})"))}\n" +
                    "QUY TẮC TRẢ LỜI:\n" +
                    "1. Trả lời bằng tiếng Việt, rõ ràng, thực tế, dùng emoji và markdown.\n" +
                    "2. Cảnh báo các mặt hàng có số lượng tồn thấp để thủ kho kịp thời nhập hàng.\n" +
                    "3. Hướng dẫn vào menu Thủ kho để tạo phiếu nhập kho hoặc phiếu xuất kho.";

                if (!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_API_KEY_HERE")
                {
                    try
                    {
                        var geminiReply = await CallGeminiWithSystemInstructionAsync(apiKey, warehouseInstruction, message);
                        if (!string.IsNullOrWhiteSpace(geminiReply)) return geminiReply;
                    }
                    catch { }
                }

                // Fallback nếu không có kết nối internet hoặc API timeout
                if (lower.Contains("tồn kho") || lower.Contains("hàng") || lower.Contains("stock"))
                {
                    var sb = new StringBuilder("📦 **Sản phẩm sắp hết hàng (≤ 10 cái):**\n");
                    if (lowStockProducts.Any())
                        foreach (var p in lowStockProducts)
                            sb.AppendLine($"- {p.Name}: còn **{p.StockQuantity}** cái");
                    else
                        sb.AppendLine("✅ Tất cả sản phẩm đều còn hàng đầy đủ.");
                    sb.AppendLine("\n👉 Vào **Quản lý kho** để cập nhật số lượng nhập hàng.");
                    return sb.ToString();
                }
                if (lower.Contains("nhập") || lower.Contains("xuất") || lower.Contains("phiếu"))
                {
                    return $"📋 **Quản lý nhập xuất kho:** Vui lòng vào **Thủ kho → Phiếu nhập/xuất kho** để tạo và duyệt phiếu.\n\n" +
                           $"💡 Tôi có thể tra cứu nhanh sản phẩm nào sắp hết hàng. Hỏi ngay nhé!";
                }
                return $"👋 Xin chào **Thủ kho**! Tôi là Trợ lý AI Kho hàng TechStore.\n\n" +
                       $"Tôi có thể hỗ trợ bạn:\n" +
                       $"- 📦 Kiểm tra **sản phẩm sắp hết hàng** ({lowStockProducts.Count} sản phẩm cần nhập)\n" +
                       $"- 📋 Hướng dẫn tạo **phiếu nhập/xuất kho**\n" +
                       $"- 🔍 Tra cứu **tồn kho theo sản phẩm**\n\n" +
                       $"Hãy hỏi tôi điều bạn cần!";
            }

            if (role == "support")
            {
                return $"🎧 Nhân viên CSKH không dùng chatbot. Hãy sử dụng **CSKH Desk** để tiếp nhận và hỗ trợ khách hàng nhé!";
            }

            return $"🤖 Xin chào! Tôi là Trợ lý AI nội bộ TechStore. Hãy hỏi tôi về công việc của bạn!";
        }

        /// <summary>
        /// BACKEND TRUY VẤN DỮ LIỆU CSDL VÀ CHUẨN BỊ NGỮ CẢNH AN TOÀN CHO AI CHATBOT
        /// AI KHÔNG TRỰC TIẾP TRUY VẤN CƠ SỞ DỮ LIỆU MÀ NHẬN DỮ LIỆU ĐƯỢC CHỌN LỌC TỪ BACKEND
        /// </summary>
        public async Task<string> GetAiResponseWithContextAsync(string message, int? userId)
        {
            var lowerMsg = message.ToLowerInvariant();

            // 1. Backend trích xuất danh mục và thương hiệu
            var categories = await _context.Categories.AsNoTracking().Select(c => c.Name).ToListAsync();
            var brands = await _context.Brands.AsNoTracking().Select(b => b.Name).ToListAsync();
            var promos = await _context.Promotions.AsNoTracking()
                .Where(p => p.EndDate >= DateTime.UtcNow && p.UsedCount < p.MaxUsage)
                .Select(p => $"Mã '{p.Code}': Giảm {(p.DiscountType == "percent" ? p.DiscountValue + "%" : p.DiscountValue.ToString("N0") + "đ")} cho đơn từ {p.MinOrderValue:N0}đ")
                .ToListAsync();

            // 2. Backend trích xuất các sản phẩm liên quan từ CSDL
            var productsQuery = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.Specs)
                .AsQueryable();

            // Tìm kiếm dựa trên từ khóa trong câu hỏi
            List<Product> relevantProducts;
            var keywords = lowerMsg.Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(k => k.Length > 1).ToList();

            var matches = await productsQuery
                .Where(p => keywords.Any(k => p.Name.ToLower().Contains(k) ||
                                              (p.Category != null && p.Category.Name.ToLower().Contains(k)) ||
                                              (p.Brand != null && p.Brand.Name.ToLower().Contains(k))))
                .Take(6)
                .ToListAsync();

            if (matches.Any())
            {
                relevantProducts = matches;
            }
            else
            {
                relevantProducts = await productsQuery
                    .OrderByDescending(p => p.RatingAvg)
                    .Take(5)
                    .ToListAsync();
            }

            // 3. Backend trích xuất thông tin đơn hàng nếu khách hỏi về đơn
            var orderInfoSb = new StringBuilder();
            var orderMatch = Regex.Match(message, @"(#?TS[0-9A-Z]+|#?DH[0-9A-Z]+)", RegexOptions.IgnoreCase);
            if (orderMatch.Success)
            {
                var code = orderMatch.Value.TrimStart('#');
                var specificOrder = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(o => o.OrderCode == code);

                if (specificOrder != null)
                {
                    orderInfoSb.AppendLine($"[THÔNG TIN ĐƠN HÀNG TRA CỨU #{specificOrder.OrderCode}]");
                    orderInfoSb.AppendLine($"- Trạng thái đơn: {TranslateOrderStatus(specificOrder.Status)}");
                    orderInfoSb.AppendLine($"- Trạng thái thanh toán: {(specificOrder.PaymentStatus == "paid" ? "Đã thanh toán" : "Chưa thanh toán")}");
                    orderInfoSb.AppendLine($"- Tổng tiền: {specificOrder.TotalAmount:N0} VNĐ");
                    orderInfoSb.AppendLine($"- Địa chỉ nhận hàng: {specificOrder.ShippingAddress}");
                    orderInfoSb.AppendLine($"- Các sản phẩm trong đơn:");
                    foreach (var it in specificOrder.OrderItems)
                    {
                        orderInfoSb.AppendLine($"  + {it.Product?.Name ?? "Sản phẩm"} x {it.Quantity} ({it.UnitPrice:N0}đ)");
                    }
                }
            }
            else if (userId.HasValue && (lowerMsg.Contains("đơn hàng") || lowerMsg.Contains("đơn mua") || lowerMsg.Contains("tình trạng đơn")))
            {
                var userOrders = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.UserId == userId.Value)
                    .OrderByDescending(o => o.OrderedAt)
                    .Take(3)
                    .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                    .ToListAsync();

                if (userOrders.Any())
                {
                    orderInfoSb.AppendLine("[DANH SÁCH ĐƠN HÀNG GẦN NHẤT CỦA KHÁCH HÀNG]");
                    foreach (var o in userOrders)
                    {
                        orderInfoSb.AppendLine($"- Đơn #{o.OrderCode}: Trạng thái: {TranslateOrderStatus(o.Status)}, Thanh toán: {(o.PaymentStatus == "paid" ? "Đã TT" : "Chưa TT")}, Tổng tiền: {o.TotalAmount:N0}đ");
                    }
                }
            }

            // 4. Định dạng dữ liệu CSDL thành Context có cấu trúc
            var contextSb = new StringBuilder();
            contextSb.AppendLine("=== DỮ LIỆU CỬA HÀNG TECHSTORE ĐƯỢC CUNG CẤP TỪ BACKEND ===");
            contextSb.AppendLine($"Danh mục hiện có: {string.Join(", ", categories)}");
            contextSb.AppendLine($"Thương hiệu kinh doanh: {string.Join(", ", brands)}");
            contextSb.AppendLine($"Chương trình khuyến mãi: {(promos.Any() ? string.Join("; ", promos) : "Miễn phí vận chuyển toàn quốc cho đơn từ 500k")}");
            contextSb.AppendLine("Chính sách: Bảo hành chính hãng 12-24 tháng, 1 đổi 1 trong 30 ngày, đổi trả miễn phí.");
            contextSb.AppendLine("Thanh toán chuyển khoản: Ngân hàng Quân Đội (MB Bank) - STK: 1220820056666 - Chủ TK: Hà Phương Nam.");
            contextSb.AppendLine("Sản phẩm liên quan từ CSDL:");
            foreach (var p in relevantProducts)
            {
                var priceStr = p.SalePrice > 0 && p.SalePrice < p.Price 
                    ? $"{p.SalePrice:N0}đ (Giá gốc: {p.Price:N0}đ, Giảm {Math.Round((1 - p.SalePrice/p.Price)*100)}%)" 
                    : $"{p.Price:N0}đ";
                var specStr = p.Specs.Any() ? string.Join(", ", p.Specs.Take(3).Select(s => $"{s.SpecKey}: {s.SpecValue}")) : "";
                contextSb.AppendLine($"- [{p.Category?.Name ?? "Sản phẩm"}] {p.Name} | Giá: {priceStr} | Đánh giá: {p.RatingAvg}★ | Còn: {p.StockQuantity} cái | {specStr}");
            }

            if (orderInfoSb.Length > 0)
            {
                contextSb.AppendLine();
                contextSb.Append(orderInfoSb);
            }

            var storeContext = contextSb.ToString();

            // 5. Thử gọi mô hình Gemini với ngữ cảnh được Backend cung cấp
            var apiKey = _configuration["GeminiApiKey"];
            if (!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_API_KEY_HERE")
            {
                try
                {
                    var geminiResponse = await CallGeminiWithContextAsync(apiKey, storeContext, message);
                    if (!string.IsNullOrWhiteSpace(geminiResponse))
                    {
                        return geminiResponse;
                    }
                }
                catch
                {
                    // Fallback to internal smart assistant
                }
            }

            // 6. Intelligent Internal Assistant Fallback (Chạy trực tiếp từ Backend đảm bảo 100% chính xác dữ liệu CSDL)
            return GenerateInternalSmartAssistantReply(message, relevantProducts, orderInfoSb.ToString(), promos, categories);
        }

        private async Task<string?> CallGeminiWithContextAsync(string apiKey, string storeContext, string userMessage)
        {
            var systemInstruction = 
                "Bạn là Trợ lý AI thông minh và thân thiện của TechStore.\n" +
                "QUAN TRỌNG: Bạn KHÔNG truy cập trực tiếp vào CSDL mà nhận dữ liệu được Backend trích xuất an toàn sau đây:\n" +
                storeContext + "\n" +
                "Quy tắc trả lời:\n" +
                "1. Trả lời bằng tiếng Việt, ngắn gọn, dùng biểu tượng cảm xúc (emoji).\n" +
                "2. Báo đúng giá, thông số và trạng thái còn hàng theo dữ liệu backend cung cấp.\n" +
                "3. Nếu khách tra cứu đơn hàng, báo chính xác tình trạng đơn dựa vào mục [THÔNG TIN ĐƠN HÀNG].\n" +
                "4. Nếu khách muốn gặp người thật, hướng dẫn khách chuyển sang tab 'Nhân viên CSKH' bên cạnh.";

            return await CallGeminiWithSystemInstructionAsync(apiKey, systemInstruction, userMessage);
        }

        private async Task<string?> CallGeminiWithSystemInstructionAsync(string apiKey, string systemInstruction, string userMessage)
        {
            var candidateModels = new[] { "gemini-3.5-flash", "gemini-3-flash-preview", "gemini-3.5-flash-lite", "gemini-flash-latest" };

            foreach (var model in candidateModels)
            {
                try
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                    var requestBody = new
                    {
                        systemInstruction = new
                        {
                            parts = new[] { new { text = systemInstruction } }
                        },
                        contents = new[]
                        {
                            new
                            {
                                parts = new[] { new { text = userMessage } }
                            }
                        }
                    };

                    var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(url, content);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var reply = doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text").GetString();

                        if (!string.IsNullOrWhiteSpace(reply)) return reply;
                    }
                }
                catch
                {
                    // Tiếp tục thử model kế tiếp
                }
            }

            return null;
        }

        private string GenerateInternalSmartAssistantReply(string message, List<Product> products, string orderInfo, List<string> promos, List<string> categories)
        {
            var lower = message.ToLowerInvariant();

            // 1. Phản hồi tra cứu đơn hàng
            if (!string.IsNullOrWhiteSpace(orderInfo))
            {
                return $"🤖 **Trợ lý AI TechStore đã tra cứu thông tin đơn hàng từ hệ thống:**\n\n{orderInfo}\n\n👉 Bạn có thể theo dõi hành trình chi tiết trong mục **'Đơn mua của tôi'** hoặc nhắn tin với nhân viên CSKH nếu cần thay đổi địa chỉ!";
            }

            // 2. Phản hồi về thanh toán / chuyển khoản
            if (lower.Contains("stk") || lower.Contains("số tài khoản") || lower.Contains("ngân hàng") || lower.Contains("chuyển khoản") || lower.Contains("thanh toán"))
            {
                return "💳 **Thông tin thanh toán chuyển khoản chính thức của TechStore:**\n" +
                       "- **Ngân hàng:** MB Bank (Ngân hàng Quân Đội)\n" +
                       "- **Số tài khoản:** `1220820056666`\n" +
                       "- **Chủ tài khoản:** Hà Phương Nam\n" +
                       "- **Nội dung CK:** Mã đơn hàng của bạn (VD: `DH123456`)\n\n" +
                       "*(Hệ thống sẽ tự động đối soát và xác nhận đơn ngay khi nhận được tiền ạ!)*";
            }

            // 3. Phản hồi về mã giảm giá / khuyến mãi
            if (lower.Contains("khuyến mãi") || lower.Contains("mã giảm giá") || lower.Contains("voucher") || lower.Contains("sale"))
            {
                var promoText = promos.Any() ? string.Join("\n- ", promos) : "- Miễn phí vận chuyển toàn quốc cho mọi đơn hàng từ 500.000đ";
                return $"🎁 **Các chương trình ưu đãi hiện có tại TechStore:**\n- {promoText}\n\n👉 Hãy nhập mã vào ô Khuyến Mãi lúc thanh toán để được giảm giá ngay nhé!";
            }

            // 4. Phản hồi về chính sách bảo hành, đổi trả
            if (lower.Contains("bảo hành") || lower.Contains("đổi trả") || lower.Contains("chính sách"))
            {
                return "🛡️ **Chính sách bảo hành & hậu mãi tại TechStore:**\n" +
                       "- **Bảo hành chính hãng:** 12 - 24 tháng cho tất cả thiết bị điện thoại, laptop, phụ kiện.\n" +
                       "- **Đổi mới 1 - 1:** Miễn phí trong 30 ngày đầu nếu phát sinh lỗi phần cứng từ nhà sản xuất.\n" +
                       "- **Hỗ trợ kỹ thuật trọn đời:** Cài đặt phần mềm, vệ sinh máy hoàn toàn miễn phí.\n" +
                       "- **Hotline CSKH:** 1900-6868 (8:00 - 21:30 hàng ngày).";
            }

            // 5. Phản hồi tư vấn sản phẩm từ dữ liệu CSDL được Backend trích xuất
            if (products.Any())
            {
                var sb = new StringBuilder();
                sb.AppendLine("🤖 **Dưới đây là các sản phẩm phù hợp nhất đang có tại TechStore:**\n");
                foreach (var p in products.Take(4))
                {
                    var price = p.SalePrice > 0 && p.SalePrice < p.Price 
                        ? $"**{p.SalePrice:N0}đ** *(giảm từ {p.Price:N0}đ)*" 
                        : $"**{p.Price:N0}đ**";
                    sb.AppendLine($"🔹 **{p.Name}**");
                    sb.AppendLine($"   - Giá ưu đãi: {price} | Đánh giá: {p.RatingAvg}⭐");
                    sb.AppendLine($"   - Tình trạng: {(p.StockQuantity > 0 ? $"Còn hàng ({p.StockQuantity} cái)" : "Tạm hết hàng")}");
                    if (p.Specs.Any())
                    {
                        var topSpecs = string.Join(", ", p.Specs.Take(2).Select(s => $"{s.SpecKey}: {s.SpecValue}"));
                        sb.AppendLine($"   - Cấu hình nổi bật: {topSpecs}");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine("👉 Bạn có thể bấm trực tiếp vào sản phẩm trên gian hàng để xem chi tiết hoặc đặt mua ngay nhé!");
                return sb.ToString();
            }

            // 6. Lời chào mặc định
            return "👋 Chào bạn! Mình là Trợ lý AI của TechStore.\n" +
                   "Mình có thể giúp bạn:\n" +
                   "1. 📱 **Tư vấn sản phẩm & báo giá** (iPhone, Samsung, Laptop, Tai nghe...)\n" +
                   "2. 📦 **Tra cứu đơn hàng** (nhập mã đơn như `#DH123456`)\n" +
                   "3. 🎟️ **Cập nhật mã giảm giá & khuyến mãi mới nhất**\n" +
                   "4. 💳 **Thông tin thanh toán & bảo hành**\n\n" +
                   "Nếu bạn muốn trao đổi trực tiếp với nhân viên bán hàng, hãy bấm chuyển sang tab **'Nhân viên CSKH'** ở phía trên nhé!";
        }

        private static string TranslateOrderStatus(string status)
        {
            return status?.ToLower() switch
            {
                "pending"    => "⏳ Chờ xác nhận",
                "processing" => "⚙️ Đang chuẩn bị hàng",
                "shipped"    => "🚚 Đang giao hàng",
                "delivered"  => "✅ Đã giao thành công",
                "cancelled"  => "❌ Đã hủy",
                _            => status ?? "Đang xử lý"
            };
        }
    }
}
