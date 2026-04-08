using Eshop.Constants;
using Eshop.Helpers;
using Eshop.Models;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Eshop.Services
{
    public class CatalogCacheService : ICatalogCacheService
    {
        private readonly DataContext _context;
        private readonly HybridCache _cache;
        private readonly ILogger<CatalogCacheService> _logger;

        public CatalogCacheService(
            DataContext context,
            HybridCache cache,
            ILogger<CatalogCacheService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<CategoryModel>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            const string cacheKey = "catalog_categories";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async cancel =>
                {
                    _logger.LogInformation("CACHE MISS: {CacheKey}", cacheKey);

                    return await _context.Categories
                        .AsNoTracking()
                        .Where(x => x.Status == 1)
                        .OrderBy(x => x.Name)
                        .ToListAsync(cancel);
                },
                options: new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(30),
                    LocalCacheExpiration = TimeSpan.FromMinutes(10)
                },
                cancellationToken: cancellationToken);
        }

        public async Task<List<PublisherModel>> GetPublishersAsync(CancellationToken cancellationToken = default)
        {
            const string cacheKey = "catalog_publishers";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async cancel =>
                {
                    _logger.LogInformation("CACHE MISS: {CacheKey}", cacheKey);

                    return await _context.Publishers
                        .AsNoTracking()
                        .OrderBy(x => x.Name)
                        .ToListAsync(cancel);
                },
                options: new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(30),
                    LocalCacheExpiration = TimeSpan.FromMinutes(10)
                },
                cancellationToken: cancellationToken);
        }

        public async Task<List<ProductModel>> GetFeaturedProductsAsync(int count = 12, CancellationToken cancellationToken = default)
        {
            string cacheKey = $"featured_products_{count}";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async cancel =>
                {
                    _logger.LogInformation("CACHE MISS: {CacheKey}", cacheKey);

                    return await _context.Products
                        .AsNoTracking()
                        .Include(x => x.Category)
                        .Include(x => x.Publisher)
                        .WhereVisibleOnStorefront(_context)
                        .OrderByDescending(x => x.Id)
                        .Take(count)
                        .ToListAsync(cancel);
                },
                options: new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(5),
                    LocalCacheExpiration = TimeSpan.FromMinutes(2)
                },
                cancellationToken: cancellationToken);
        }

        public async Task<ProductModel?> GetProductByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            string cacheKey = $"product_detail_{id}";

            return await _cache.GetOrCreateAsync(
                cacheKey,
                async cancel =>
                {
                    _logger.LogInformation("CACHE MISS: {CacheKey}", cacheKey);

                    return await _context.Products
                        .AsNoTracking()
                        .Include(x => x.Category)
                        .Include(x => x.Publisher)
                        .Include(x => x.OptionGroups)
                            .ThenInclude(g => g.OptionValues)
                        .WhereVisibleOnStorefront(_context)
                        .FirstOrDefaultAsync(x => x.Id == id, cancel);
                },
                options: new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(10),
                    LocalCacheExpiration = TimeSpan.FromMinutes(5)
                },
                cancellationToken: cancellationToken);
        }

        public async Task RemoveCategoriesAsync(CancellationToken cancellationToken = default)
            => await _cache.RemoveAsync("catalog_categories", cancellationToken);

        public async Task RemovePublishersAsync(CancellationToken cancellationToken = default)
            => await _cache.RemoveAsync("catalog_publishers", cancellationToken);

        public async Task RemoveProductAsync(long id, CancellationToken cancellationToken = default)
            => await _cache.RemoveAsync($"product_detail_{id}", cancellationToken);

        public ValueTask RemoveHomeProductsAsync(CancellationToken cancellationToken = default)
            => _cache.RemoveAsync(CacheKeys.HomeProducts, cancellationToken);

        public async Task RemoveFeaturedProductsAsync(CancellationToken cancellationToken = default)
        {
            for (int count = 1; count <= 24; count++)
            {
                await _cache.RemoveAsync($"featured_products_{count}", cancellationToken);
            }
        }

        public async Task RemoveAllCatalogCachesAsync(CancellationToken cancellationToken = default)
        {
            await RemoveCategoriesAsync(cancellationToken);
            await RemovePublishersAsync(cancellationToken);
            await RemoveFeaturedProductsAsync(cancellationToken);
        }

        public async Task<List<ProductModel>> GetHomeProductsAsync(CancellationToken cancellationToken = default)
        {
            return await _cache.GetOrCreateAsync(
                CacheKeys.HomeProducts,
                async cancel =>
                {
                    _logger.LogInformation("CACHE MISS: {CacheKey}", CacheKeys.HomeProducts);

                    return await _context.Products
                        .AsNoTracking()
                        .Include(p => p.Publisher)
                        .Include(p => p.Category)
                        .Include(p => p.ProductImages)
                        .WhereVisibleOnStorefront(_context)
                        .ToListAsync(cancel);
                },
                options: new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(5),
                    LocalCacheExpiration = TimeSpan.FromMinutes(2)
                },
                cancellationToken: cancellationToken);
        }
    }
}