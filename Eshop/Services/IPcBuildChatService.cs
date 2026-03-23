using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    public interface IPcBuildChatService
    {
        Task<PcBuildChatResponse> AskAsync(PcBuildChatRequest request);
    }
}
