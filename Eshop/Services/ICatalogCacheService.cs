using Eshop.Models;

namespace Eshop.Services
{
    public interface ICatalogCacheService
    {
        Task<List<CategoryModel>> GetCategoriesAsync(); 
        Task<List<PublisherModel>> GetPublishersAsync();
        Task<List<ProductModel>> GetFeaturedProductsAsync(int count = 12);
        Task<ProductModel?> GetProductByIdAsync(long id);

        void RemoveCategories();
        void RemovePublishers();
        void RemoveFeaturedProducts();
        void RemoveProduct(long id);
        void RemoveAllCatalogCaches();
    }
}