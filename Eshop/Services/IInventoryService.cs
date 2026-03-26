using Eshop.Models.ViewModels;
using System.Security.Claims;

namespace Eshop.Services
{
    public interface IInventoryService
    {
        Task<int> GetAvailableStockAsync(int productId);

        Task<int> CreateReceiptAsync(AdminInventoryReceiveViewModel vm, string? userId);
        Task ApproveReceiptAsync(int receiptId, string? userId);
        Task CancelReceiptAsync(int receiptId, string? userId, string? note = null);

        Task AdjustAsync(AdminInventoryAdjustViewModel vm, string? userId);
        Task TransferAsync(AdminInventoryTransferViewModel vm, string? userId);
        Task BootstrapLegacyStockAsync(string? userId);

        Task IssueOrderAsync(string orderCode, Dictionary<int, int> requestedQtyByProduct, string? userId);
        Task ReturnOrderAsync(string orderCode, string? userId, string? note = null);
        Task RevertOrderInventoryAsync(string orderCode, string? reservationCode, string? userId, string? note = null);

        Task CleanupExpiredReservationsAsync(string? userId = null);
        Task<string> ReserveCartAsync(HttpContext httpContext, ClaimsPrincipal user, string paymentMethod, int expireMinutes = 20);
        Task CommitReservationAsync(string reservationCode, string orderCode, string? userId);
        Task ReleaseReservationAsync(string reservationCode, string? userId, string? note = null, bool expired = false);
    }
}
