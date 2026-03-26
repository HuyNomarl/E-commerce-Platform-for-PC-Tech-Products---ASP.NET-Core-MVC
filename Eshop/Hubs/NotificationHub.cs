using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Eshop.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            if (Context.User?.IsInRole(Eshop.Constants.RoleNames.Admin) == true ||
                Context.User?.IsInRole(Eshop.Constants.RoleNames.OrderStaff) == true)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    Eshop.Constants.NotificationGroups.OrderManagers);
            }

            if (Context.User?.IsInRole(Eshop.Constants.RoleNames.Admin) == true ||
                Context.User?.IsInRole(Eshop.Constants.RoleNames.SupportStaff) == true)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    Eshop.Constants.NotificationGroups.SupportAgents);
            }

            await base.OnConnectedAsync();
        }
    }
}
