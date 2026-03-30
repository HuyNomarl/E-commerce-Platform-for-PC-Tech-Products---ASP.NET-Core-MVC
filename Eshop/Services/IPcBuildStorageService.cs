using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    public interface IPcBuildStorageService
    {
        Task<PcBuilderBuildDetailDto> BuildDetailAsync(string? buildName, IReadOnlyCollection<PcBuildCheckItemDto> items);
        Task<PcBuildImportResultDto> ResolveImportedRowsAsync(string? buildName, IReadOnlyCollection<PcBuildWorkbookRowModel> rows);
        Task<PcBuildSaveResultDto> SaveAsync(string? buildName, IReadOnlyCollection<PcBuildCheckItemDto> items, string? userId, bool allowInvalidBuild);
        Task<PcBuilderBuildDetailDto?> GetBuildDetailByIdAsync(int buildId);
        Task<PcBuilderBuildDetailDto?> GetBuildDetailByCodeAsync(string buildCode);
    }
}
