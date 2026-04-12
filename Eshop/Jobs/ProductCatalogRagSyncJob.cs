using Eshop.Models.Configurations;
using Eshop.Services;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Eshop.Jobs
{
    public class ProductCatalogRagSyncJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RagServiceOptions _options;
        private readonly ILogger<ProductCatalogRagSyncJob> _logger;

        public ProductCatalogRagSyncJob(
            IServiceScopeFactory scopeFactory,
            IOptions<RagServiceOptions> options,
            ILogger<ProductCatalogRagSyncJob> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 2)]
        [DisableConcurrentExecution(timeoutInSeconds: 1800)]
        public async Task RunFullSyncAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IProductCatalogRagSyncService>();
            var report = await syncService.SyncAllAsync();

            if (!report.Success)
            {
                var message = string.Join(" | ", report.Errors);
                _logger.LogWarning("Catalog -> RAG full sync failed. Errors: {Errors}", message);
                throw new InvalidOperationException($"Catalog -> RAG full sync failed: {message}");
            }

            _logger.LogInformation(
                "Catalog -> RAG full sync completed. Upserted={UpsertedCount}, Deleted={DeletedCount}",
                report.UpsertedCount,
                report.DeletedCount);
        }

        [AutomaticRetry(Attempts = 2)]
        [DisableConcurrentExecution(timeoutInSeconds: 600)]
        public async Task SyncProductsAsync(List<int> productIds)
        {
            var distinctIds = (productIds ?? new List<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (!distinctIds.Any())
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IProductCatalogRagSyncService>();

            foreach (var productId in distinctIds)
            {
                var synced = await syncService.SyncProductAsync(productId);
                if (!synced)
                {
                    throw new InvalidOperationException($"Catalog -> RAG product sync failed for product {productId}.");
                }
            }

            _logger.LogInformation(
                "Catalog -> RAG incremental sync completed. ProductCount={ProductCount}",
                distinctIds.Count);
        }

        public bool IsStartupSyncEnabled() => _options.StartupFullSyncEnabled;
    }
}
