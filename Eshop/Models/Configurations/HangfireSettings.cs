namespace Eshop.Models.Configurations
{
    public class HangfireSettings
    {
        public string DashboardPath { get; set; } = "/Admin/Jobs";
        public string InventoryReservationCleanupCron { get; set; } = "*/5 * * * *";
        public string CatalogRecurringSyncCron { get; set; } = string.Empty;
        public int QueuePollIntervalSeconds { get; set; } = 15;
    }
}
