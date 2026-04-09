namespace Eshop.Models.Configurations
{
    public class RagServiceOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:8001";
        public string CatalogNamespace { get; set; } = "catalog_products";
        public bool StartupFullSyncEnabled { get; set; } = true;
        public int StartupInitialDelaySeconds { get; set; } = 15;
        public int StartupRetryCount { get; set; } = 3;
    }
}
