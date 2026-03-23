using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    public interface IBuildRequirementExtractor
    {
        Task<BuildRequirementProfile> ExtractAsync(string userMessage);
    }
}
