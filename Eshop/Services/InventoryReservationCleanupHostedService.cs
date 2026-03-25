using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eshop.Services
{
    public class InventoryReservationCleanupHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<InventoryReservationCleanupHostedService> _logger;

        public InventoryReservationCleanupHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<InventoryReservationCleanupHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

                    await inventoryService.CleanupExpiredReservationsAsync("SYSTEM");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi dọn reservation hết hạn.");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
