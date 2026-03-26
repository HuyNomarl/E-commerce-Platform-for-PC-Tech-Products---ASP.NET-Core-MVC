using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eshop.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly DataContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;
        private readonly UserManager<AppUserModel> _userManager;

        public ChatHub(
            DataContext context,
            IHubContext<NotificationHub> notificationHub,
            UserManager<AppUserModel> userManager)
        {
            _context = context;
            _notificationHub = notificationHub;
            _userManager = userManager;
        }

        public async Task SendMessage(string receiverId, string content)
        {
            var senderId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(senderId))
                throw new HubException("Bạn chưa đăng nhập.");

            if (string.IsNullOrWhiteSpace(receiverId))
                throw new HubException("Không có người nhận.");

            if (string.IsNullOrWhiteSpace(content))
                throw new HubException("Tin nhắn trống.");

            content = content.Trim();

            var sender = await _context.Users.FirstOrDefaultAsync(x => x.Id == senderId);
            var receiver = await _context.Users.FirstOrDefaultAsync(x => x.Id == receiverId);

            if (sender == null || receiver == null)
                throw new HubException("Người gửi hoặc người nhận không tồn tại.");

            var senderRoles = await _userManager.GetRolesAsync(sender);
            var receiverRoles = await _userManager.GetRolesAsync(receiver);

            var senderIsSupport = senderRoles.Any(role =>
                string.Equals(role, Eshop.Constants.RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, Eshop.Constants.RoleNames.SupportStaff, StringComparison.OrdinalIgnoreCase));
            var senderIsCustomer = senderRoles.Any(role =>
                    string.Equals(role, Eshop.Constants.RoleNames.Customer, StringComparison.OrdinalIgnoreCase))
                && !senderRoles.Any(Eshop.Constants.RoleNames.IsBackOfficeRole);

            var receiverIsSupport = receiverRoles.Any(role =>
                string.Equals(role, Eshop.Constants.RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, Eshop.Constants.RoleNames.SupportStaff, StringComparison.OrdinalIgnoreCase));
            var receiverIsCustomer = receiverRoles.Any(role =>
                    string.Equals(role, Eshop.Constants.RoleNames.Customer, StringComparison.OrdinalIgnoreCase))
                && !receiverRoles.Any(Eshop.Constants.RoleNames.IsBackOfficeRole);

            if (!senderIsSupport && !senderIsCustomer)
                throw new HubException("Tài khoản này không được phép sử dụng kênh chat hỗ trợ.");

            if (senderIsSupport && !receiverIsCustomer)
                throw new HubException("Nhân viên hỗ trợ chỉ được chat với khách hàng.");

            if (senderIsCustomer && !receiverIsSupport)
                throw new HubException("Khách hàng chỉ được chat với bộ phận hỗ trợ.");

            var message = new MessageModel
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var payload = new
            {
                id = message.Id,
                senderId = senderId,
                senderName = sender.UserName,
                receiverId = receiverId,
                receiverName = receiver.UserName,
                content = message.Content,
                createdAt = message.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            };

            await Clients.User(senderId).SendAsync("ReceiveSupportMessage", payload);
            await Clients.User(receiverId).SendAsync("ReceiveSupportMessage", payload);

            if (senderIsCustomer)
            {
                await _notificationHub.Clients
                    .Group(Eshop.Constants.NotificationGroups.SupportAgents)
                    .SendAsync("NewChatNotification", new
                {
                    fromUserId = senderId,
                    fromUserName = sender.UserName ?? "Khách hàng",
                    preview = message.Content,
                    createdAt = payload.createdAt,
                    url = "/Admin/Chat"
                });
            }
        }
    }
}
