using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eshop.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly DataContext _context;

        public ChatHub(DataContext context)
        {
            _context = context;
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
        }
    }
}