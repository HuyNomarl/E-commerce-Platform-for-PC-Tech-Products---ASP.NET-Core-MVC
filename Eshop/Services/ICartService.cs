using Eshop.Models;

namespace Eshop.Services
{
    public interface ICartService
    {
        Task<List<CartItemModel>> GetCartAsync(HttpContext httpContext, string? userId = null, bool mergeSessionCart = true);
        Task SaveCartAsync(HttpContext httpContext, IReadOnlyCollection<CartItemModel> cartItems, string? userId = null);
        Task ClearCartAsync(HttpContext httpContext, string? userId = null);
        Task MergeSessionCartAsync(HttpContext httpContext, string? userId = null);
        Task RemovePurchasedItemsAsync(HttpContext httpContext, IReadOnlyCollection<CartItemModel> purchasedItems, string? userId = null);
    }
}
