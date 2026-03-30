using Eshop.Models.ViewModels;

namespace Eshop.Services
{
    public interface IPcBuildWorkbookService
    {
        byte[] Export(PcBuilderBuildDetailDto build);
        Task<PcBuildWorkbookImportModel> ImportAsync(Stream stream, CancellationToken cancellationToken = default);
    }
}
