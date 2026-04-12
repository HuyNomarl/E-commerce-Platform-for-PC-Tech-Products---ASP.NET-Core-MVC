using Eshop.Services;
using Hangfire;

namespace Eshop.Jobs
{
    public class InventoryReservationCleanupJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<InventoryReservationCleanupJob> _logger;

        public InventoryReservationCleanupJob(
            IServiceScopeFactory scopeFactory,
            ILogger<InventoryReservationCleanupJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)]
        [DisableConcurrentExecution(timeoutInSeconds: 600)]
        public async Task RunAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

            await inventoryService.CleanupExpiredReservationsAsync("SYSTEM");

            _logger.LogInformation("Inventory reservation cleanup completed successfully.");
        }
    }
}
