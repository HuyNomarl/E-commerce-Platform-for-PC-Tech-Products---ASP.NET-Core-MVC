using Eshop.Models.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eshop.Services
{
    public class ProductCatalogRagSyncHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RagServiceOptions _options;
        private readonly ILogger<ProductCatalogRagSyncHostedService> _logger;

        public ProductCatalogRagSyncHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<RagServiceOptions> options,
            ILogger<ProductCatalogRagSyncHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.StartupFullSyncEnabled)
            {
                _logger.LogInformation("Startup full-sync catalog -> RAG is disabled.");
                return;
            }

            var delaySeconds = Math.Max(0, _options.StartupInitialDelaySeconds);
            if (delaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }

            var retries = Math.Max(1, _options.StartupRetryCount);

            for (var attempt = 1; attempt <= retries && !stoppingToken.IsCancellationRequested; attempt++)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var syncService = scope.ServiceProvider.GetRequiredService<IProductCatalogRagSyncService>();
                    var report = await syncService.SyncAllAsync(stoppingToken);

                    if (report.Success)
                    {
                        _logger.LogInformation(
                            "Startup full-sync catalog -> RAG succeeded. Upserted={UpsertedCount}, Deleted={DeletedCount}",
                            report.UpsertedCount,
                            report.DeletedCount);
                        return;
                    }

                    _logger.LogWarning(
                        "Startup full-sync catalog -> RAG failed on attempt {Attempt}/{RetryCount}. Errors: {Errors}",
                        attempt,
                        retries,
                        string.Join(" | ", report.Errors));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Startup full-sync catalog -> RAG threw an exception on attempt {Attempt}/{RetryCount}.",
                        attempt,
                        retries);
                }

                if (attempt < retries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }
    }
}
