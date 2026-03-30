using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    public interface IPcBuildShareService
    {
        Task<List<PcBuildShareUserLookupDto>> SearchReceiversAsync(string currentUserId, string? keyword, int limit = 8);
        Task<PcBuildShareCreatedDto> ShareAsync(string senderUserId, PcBuildShareRequest request);
        Task<List<PcBuildShareListItemDto>> GetReceivedSharesAsync(string receiverUserId, int take = 20);
        Task<PcBuilderBuildDetailDto?> GetSharedBuildAsync(string receiverUserId, string shareCode);
    }
}
