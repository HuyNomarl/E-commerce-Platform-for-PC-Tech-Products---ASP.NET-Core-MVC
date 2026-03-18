using Eshop.Models;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Eshop.Services
{
    public class CatalogCacheService : ICatalogCacheService
    {
        private readonly DataContext _context;
        private readonly IMemoryCache _cache;

        public CatalogCacheService(DataContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<List<CategoryModel>> GetCategoriesAsync()
        {
            const string cacheKey = "catalog_categories";

            if (_cache.TryGetValue(cacheKey, out List<CategoryModel>? data) && data != null)
                return data;

            data = await _context.Categories
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            _cache.Set(cacheKey, data, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                SlidingExpiration = TimeSpan.FromMinutes(10)
            });

            return data;
        }

        public async Task<List<PublisherModel>> GetPublishersAsync()
        {
            const string cacheKey = "catalog_publishers";

            if (_cache.TryGetValue(cacheKey, out List<PublisherModel>? data) && data != null)
                return data;

            data = await _context.Publishers
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            _cache.Set(cacheKey, data, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                SlidingExpiration = TimeSpan.FromMinutes(10)
            });

            return data;
        }

        public async Task<List<ProductModel>> GetFeaturedProductsAsync(int count = 12)
        {
            string cacheKey = $"featured_products_{count}";

            if (_cache.TryGetValue(cacheKey, out List<ProductModel>? data) && data != null)
                return data;

            data = await _context.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Publisher   )
                .OrderByDescending(x => x.Id)
                .Take(count)
                .ToListAsync();

            _cache.Set(cacheKey, data, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            return data;
        }

        public async Task<ProductModel?> GetProductByIdAsync(long id)
        {
            string cacheKey = $"product_detail_{id}";

            if (_cache.TryGetValue(cacheKey, out ProductModel? data) && data != null)
                return data;

            data = await _context.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Publisher)
                .Include(x => x.OptionGroups)
                    .ThenInclude(g => g.OptionValues)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (data != null)
            {
                _cache.Set(cacheKey, data, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(5)
                });
            }

            return data;
        }

        public void RemoveCategories() => _cache.Remove("catalog_categories");
        public void RemovePublishers() => _cache.Remove("catalog_publishers");
        public void RemoveFeaturedProducts() => _cache.Remove("featured_products_12");
        public void RemoveProduct(long id) => _cache.Remove($"product_detail_{id}");

        public void RemoveAllCatalogCaches()
        {
            RemoveCategories();
            RemovePublishers();
            RemoveFeaturedProducts();
        }
    }
}