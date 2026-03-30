using Eshop.Constants;
using Eshop.Models.ViewModels;
using Eshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Eshop.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ISupportChatService _supportChatService;

        public ChatHub(ISupportChatService supportChatService)
        {
            _supportChatService = supportChatService;
        }

        public override async Task OnConnectedAsync()
        {
            if (Context.User?.IsInRole(RoleNames.Admin) == true ||
                Context.User?.IsInRole(RoleNames.SupportStaff) == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.SupportAgents);
            }

            await base.OnConnectedAsync();
        }

        public async Task SendMessage(string receiverId, string content)
        {
            var senderId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(senderId))
                throw new HubException("Ban chua dang nhap.");

            if (string.IsNullOrWhiteSpace(receiverId))
                throw new HubException("Khong co nguoi nhan.");

            if (string.IsNullOrWhiteSpace(content))
                throw new HubException("Tin nhan trong.");

            try
            {
                await _supportChatService.SendAsync(senderId, receiverId, new SupportChatSendInputViewModel
                {
                    ReceiverId = receiverId,
                    Content = content.Trim()
                });
            }
            catch (InvalidOperationException ex)
            {
                throw new HubException(ex.Message);
            }
        }
    }
}
