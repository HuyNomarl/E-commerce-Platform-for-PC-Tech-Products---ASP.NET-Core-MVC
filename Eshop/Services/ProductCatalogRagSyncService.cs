using Eshop.Models;
using Eshop.Models.Configurations;
using Eshop.Models.Enums;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eshop.Services
{
    public class ProductCatalogRagSyncService : IProductCatalogRagSyncService
    {
        private readonly DataContext _context;
        private readonly RagClient _ragClient;
        private readonly RagServiceOptions _options;
        private readonly ILogger<ProductCatalogRagSyncService> _logger;

        public ProductCatalogRagSyncService(
            DataContext context,
            RagClient ragClient,
            IOptions<RagServiceOptions> options,
            ILogger<ProductCatalogRagSyncService> logger)
        {
            _context = context;
            _ragClient = ragClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<bool> SyncProductAsync(int productId, CancellationToken cancellationToken = default)
        {
            try
            {
                var product = await LoadProductAsync(productId, cancellationToken);
                if (product == null)
                {
                    await DeleteProductAsync(productId, cancellationToken);
                    return true;
                }

                if (!ShouldSyncProduct(product))
                {
                    await DeleteProductAsync(product.Id, cancellationToken);
                    return true;
                }

                var availableStock = await GetAvailableStockMapAsync(new[] { product.Id }, cancellationToken);
                await _ragClient.UpsertDocumentAsync(
                    BuildUpsertRequest(product, availableStock.GetValueOrDefault(product.Id)),
                    cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Khong the dong bo san pham {ProductId} sang RAG.", productId);
                return false;
            }
        }

        public async Task<bool> DeleteProductAsync(int productId, CancellationToken cancellationToken = default)
        {
            try
            {
                await _ragClient.DeleteDocumentAsync(_options.CatalogNamespace, BuildDocumentId(productId), cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Khong the xoa san pham {ProductId} khoi RAG.", productId);
                return false;
            }
        }

        public async Task<ProductCatalogRagSyncReport> SyncAllAsync(CancellationToken cancellationToken = default)
        {
            var report = new ProductCatalogRagSyncReport();

            try
            {
                var products = await _context.Products
                    .AsNoTracking()
                    .Include(x => x.Category)
                    .Include(x => x.Publisher)
                    .Include(x => x.Specifications)
                        .ThenInclude(x => x.SpecificationDefinition)
                    .ToListAsync(cancellationToken);

                var stockMap = await GetAvailableStockMapAsync(products.Select(x => x.Id), cancellationToken);
                var existing = await _ragClient.ListDocumentsAsync(_options.CatalogNamespace, cancellationToken)
                    ?? new RagDocumentListResponse();

                var liveDocIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var product in products)
                {
                    if (!ShouldSyncProduct(product))
                    {
                        continue;
                    }

                    await _ragClient.UpsertDocumentAsync(
                        BuildUpsertRequest(product, stockMap.GetValueOrDefault(product.Id)),
                        cancellationToken);

                    liveDocIds.Add(BuildDocumentId(product.Id));
                    report.UpsertedCount++;
                }

                foreach (var staleDocId in existing.Documents
                             .Select(x => x.DocId)
                             .Where(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith("product:", StringComparison.OrdinalIgnoreCase))
                             .Where(x => !liveDocIds.Contains(x))
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await _ragClient.DeleteDocumentAsync(_options.CatalogNamespace, staleDocId, cancellationToken);
                    report.DeletedCount++;
                }

                report.Success = true;
                return report;
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.Errors.Add(ex.Message);
                _logger.LogWarning(ex, "Khong the full-sync catalog sang RAG.");
                return report;
            }
        }

        private async Task<ProductModel?> LoadProductAsync(int productId, CancellationToken cancellationToken)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Publisher)
                .Include(x => x.Specifications)
                    .ThenInclude(x => x.SpecificationDefinition)
                .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
        }

        private async Task<Dictionary<int, int>> GetAvailableStockMapAsync(
            IEnumerable<int> productIds,
            CancellationToken cancellationToken)
        {
            var ids = productIds
                .Distinct()
                .ToList();

            if (!ids.Any())
            {
                return new Dictionary<int, int>();
            }

            return await _context.InventoryStocks
                .AsNoTracking()
                .Where(x => ids.Contains(x.ProductId) && x.Warehouse.IsActive)
                .GroupBy(x => x.ProductId)
                .Select(x => new
                {
                    ProductId = x.Key,
                    Available = x.Sum(row => row.OnHandQuantity - row.ReservedQuantity)
                })
                .ToDictionaryAsync(x => x.ProductId, x => x.Available, cancellationToken);
        }

        private RagDocumentUpsertRequest BuildUpsertRequest(ProductModel product, int availableStock)
        {
            return new RagDocumentUpsertRequest
            {
                Namespace = _options.CatalogNamespace,
                DocId = BuildDocumentId(product.Id),
                Source = BuildSource(product),
                Content = ProductRagTextFormatter.BuildCatalogDocument(product, availableStock),
                Metadata = new Dictionary<string, object?>
                {
                    ["source"] = BuildSource(product),
                    ["title"] = product.Name,
                    ["product_id"] = product.Id,
                    ["slug"] = product.Slug,
                    ["product_type"] = product.ProductType.ToString(),
                    ["component_type"] = product.ComponentType?.ToString(),
                    ["publisher"] = product.Publisher?.Name,
                    ["category"] = product.Category?.Name,
                    ["price"] = product.Price,
                    ["available_stock"] = availableStock,
                    ["status"] = product.Status,
                    ["source_type"] = "catalog_product"
                }
            };
        }

        private static string BuildDocumentId(int productId)
        {
            return $"product:{productId}";
        }

        private static string BuildSource(ProductModel product)
        {
            var slug = string.IsNullOrWhiteSpace(product.Slug) ? product.Id.ToString() : product.Slug.Trim();
            return $"catalog/products/{product.Id}-{slug}";
        }

        private static bool ShouldSyncProduct(ProductModel product)
        {
            if (product == null)
            {
                return false;
            }

            if (product.Status != 1)
            {
                return false;
            }

            if (product.Category?.Status != 1)
            {
                return false;
            }

            return product.ProductType == ProductType.Component
                   || product.ProductType == ProductType.Monitor
                   || product.ProductType == ProductType.PcPrebuilt
                   || product.IsPcBuild
                   || (product.ComponentType.HasValue && product.ComponentType.Value != PcComponentType.None);
        }
    }
}
