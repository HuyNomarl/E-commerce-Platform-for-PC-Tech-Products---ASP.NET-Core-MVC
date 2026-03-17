using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    public interface IPcCompatibilityService
    {
        Task<PcBuildCheckResponse> CheckAsync(PcBuildCheckRequest request);
    }
}