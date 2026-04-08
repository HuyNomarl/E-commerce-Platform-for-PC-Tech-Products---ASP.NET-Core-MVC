using Eshop.Models;

namespace Eshop.Services
{
    public interface ICatalogCacheService
    {
        Task<List<CategoryModel>> GetCategoriesAsync(CancellationToken cancellationToken = default);
        Task<List<PublisherModel>> GetPublishersAsync(CancellationToken cancellationToken = default);
        Task<List<ProductModel>> GetFeaturedProductsAsync(int count = 12, CancellationToken cancellationToken = default);
        Task<ProductModel?> GetProductByIdAsync(long id, CancellationToken cancellationToken = default);
        Task<List<ProductModel>> GetHomeProductsAsync(CancellationToken cancellationToken = default);

        Task RemoveCategoriesAsync(CancellationToken cancellationToken = default);
        Task RemovePublishersAsync(CancellationToken cancellationToken = default);
        Task RemoveFeaturedProductsAsync(CancellationToken cancellationToken = default);
        Task RemoveProductAsync(long id, CancellationToken cancellationToken = default);
        Task RemoveAllCatalogCachesAsync(CancellationToken cancellationToken = default);
    }
}