using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    public interface IPcBuildSuggestionService
    {
        Task<List<string>> SuggestFixesAsync(PcBuildCheckRequest request, PcBuildCheckResponse checkResult);
    }
}
