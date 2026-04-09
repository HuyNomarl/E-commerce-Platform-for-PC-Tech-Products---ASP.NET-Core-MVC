namespace Eshop.Services
{
    public interface IProductCatalogRagSyncService
    {
        Task<bool> SyncProductAsync(int productId, CancellationToken cancellationToken = default);
        Task<bool> DeleteProductAsync(int productId, CancellationToken cancellationToken = default);
        Task<ProductCatalogRagSyncReport> SyncAllAsync(CancellationToken cancellationToken = default);
    }

    public class ProductCatalogRagSyncReport
    {
        public bool Success { get; set; }
        public int UpsertedCount { get; set; }
        public int DeletedCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
