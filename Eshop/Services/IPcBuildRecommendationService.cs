using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    public interface IPcBuildRecommendationService
    {
        Task<PcBuildCheckRequest> RecommendBuildAsync(BuildRequirementProfile profile);
    }
}
