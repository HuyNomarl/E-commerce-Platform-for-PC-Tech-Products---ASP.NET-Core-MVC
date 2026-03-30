using Eshop.Models;
using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    public interface ISupportChatService
    {
        Task<SupportChatMessagePayloadViewModel> SendAsync(string senderId, string? receiverId, SupportChatSendInputViewModel input);
        Task<List<SupportChatMessagePayloadViewModel>> GetConversationForCustomerAsync(string customerId);
        Task<List<SupportChatMessagePayloadViewModel>> GetConversationForSupportAsync(string customerId);
        Task MarkConversationReadForCustomerAsync(string customerId);
        Task MarkConversationReadForSupportAsync(string customerId);
        Task<List<SupportChatConversationItemViewModel>> GetSupportConversationsAsync();
        Task<List<SupportChatProductCardViewModel>> SearchProductsAsync(string? keyword, int limit = 6);
        Task<AppUserModel?> GetPreferredSupportUserAsync(string? customerId = null);
    }
}
