using Eshop.Models;
using Eshop.Helpers;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eshop.Services
{
    public class CartService : ICartService
    {
        private const string CartSessionKey = "Cart";

        private readonly DataContext _dataContext;

        public CartService(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<List<CartItemModel>> GetCartAsync(HttpContext httpContext, string? userId = null, bool mergeSessionCart = true)
        {
            var resolvedUserId = ResolveUserId(httpContext, userId);
            if (string.IsNullOrWhiteSpace(resolvedUserId))
            {
                return await FilterVisibleCartItemsAsync(ReadSessionCart(httpContext));
            }

            if (mergeSessionCart)
            {
                await MergeSessionCartAsync(httpContext, resolvedUserId);
            }

            return await FilterVisibleCartItemsAsync(await LoadUserCartAsync(resolvedUserId));
        }

        public async Task SaveCartAsync(HttpContext httpContext, IReadOnlyCollection<CartItemModel> cartItems, string? userId = null)
        {
            var normalizedCart = NormalizeCartItems(cartItems);
            var resolvedUserId = ResolveUserId(httpContext, userId);

            if (string.IsNullOrWhiteSpace(resolvedUserId))
            {
                if (normalizedCart.Count == 0)
                {
                    httpContext.Session.Remove(CartSessionKey);
                }
                else
                {
                    httpContext.Session.SetJson(CartSessionKey, normalizedCart);
                }

                return;
            }

            await PersistUserCartAsync(resolvedUserId, normalizedCart);
            httpContext.Session.Remove(CartSessionKey);
        }

        public async Task ClearCartAsync(HttpContext httpContext, string? userId = null)
        {
            httpContext.Session.Remove(CartSessionKey);

            var resolvedUserId = ResolveUserId(httpContext, userId);
            if (string.IsNullOrWhiteSpace(resolvedUserId))
            {
                return;
            }

            var existingItems = await _dataContext.UserCartItems
                .Where(x => x.UserId == resolvedUserId)
                .ToListAsync();

            if (existingItems.Count == 0)
            {
                return;
            }

            _dataContext.UserCartItems.RemoveRange(existingItems);
            await _dataContext.SaveChangesAsync();
        }

        public async Task MergeSessionCartAsync(HttpContext httpContext, string? userId = null)
        {
            var resolvedUserId = ResolveUserId(httpContext, userId);
            if (string.IsNullOrWhiteSpace(resolvedUserId))
            {
                return;
            }

            var sessionCart = ReadSessionCart(httpContext);
            if (sessionCart.Count == 0)
            {
                return;
            }

            var existingUserCart = await LoadUserCartAsync(resolvedUserId);
            var mergedCart = MergeCarts(existingUserCart, sessionCart);

            await PersistUserCartAsync(resolvedUserId, mergedCart);
            httpContext.Session.Remove(CartSessionKey);
        }

        public async Task RemovePurchasedItemsAsync(HttpContext httpContext, IReadOnlyCollection<CartItemModel> purchasedItems, string? userId = null)
        {
            var normalizedPurchasedItems = NormalizeCartItems(purchasedItems);
            if (normalizedPurchasedItems.Count == 0)
            {
                return;
            }

            var resolvedUserId = ResolveUserId(httpContext, userId);
            if (!string.IsNullOrWhiteSpace(resolvedUserId))
            {
                await MergeSessionCartAsync(httpContext, resolvedUserId);
            }

            var currentCart = await GetCartAsync(httpContext, resolvedUserId, mergeSessionCart: false);
            if (currentCart.Count == 0)
            {
                return;
            }

            foreach (var purchasedItem in normalizedPurchasedItems)
            {
                var currentItem = currentCart.FirstOrDefault(x => string.Equals(x.LineKey, purchasedItem.LineKey, StringComparison.Ordinal));
                if (currentItem == null)
                {
                    continue;
                }

                currentItem.Quantity -= purchasedItem.Quantity;
                if (currentItem.Quantity <= 0)
                {
                    currentCart.Remove(currentItem);
                }
            }

            await SaveCartAsync(httpContext, currentCart, resolvedUserId);
        }

        private List<CartItemModel> ReadSessionCart(HttpContext httpContext)
        {
            var cartItems = httpContext.Session.GetJson<List<CartItemModel>>(CartSessionKey) ?? new List<CartItemModel>();
            return NormalizeCartItems(cartItems);
        }

        private async Task<List<CartItemModel>> LoadUserCartAsync(string userId)
        {
            var userCartItems = await _dataContext.UserCartItems
                .AsNoTracking()
                .Include(x => x.SelectedOptions)
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return userCartItems.Select(x => new CartItemModel
            {
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                Quantity = x.Quantity,
                BasePrice = x.BasePrice,
                OptionPrice = x.OptionPrice,
                Image = x.Image,
                BuildGroupKey = x.BuildGroupKey,
                PcBuildId = x.PcBuildId,
                BuildName = x.BuildName,
                IsPcBuildItem = x.IsPcBuildItem,
                ComponentType = x.ComponentType,
                SelectedOptions = x.SelectedOptions
                    .OrderBy(option => option.OptionGroupId)
                    .ThenBy(option => option.OptionValueId)
                    .Select(option => new CartItemOptionModel
                    {
                        OptionGroupId = option.OptionGroupId,
                        OptionValueId = option.OptionValueId,
                        GroupName = option.GroupName,
                        ValueName = option.ValueName,
                        AdditionalPrice = option.AdditionalPrice
                    })
                    .ToList()
            }).ToList();
        }

        private async Task PersistUserCartAsync(string userId, IReadOnlyCollection<CartItemModel> cartItems)
        {
            var existingItems = await _dataContext.UserCartItems
                .Include(x => x.SelectedOptions)
                .Where(x => x.UserId == userId)
                .ToListAsync();

            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                if (existingItems.Count > 0)
                {
                    _dataContext.UserCartItems.RemoveRange(existingItems);
                    await _dataContext.SaveChangesAsync();
                }

                if (cartItems.Count > 0)
                {
                    var now = DateTime.Now;
                    var entities = cartItems.Select(item => new UserCartItemModel
                    {
                        UserId = userId,
                        ProductId = checked((int)item.ProductId),
                        ProductName = item.ProductName ?? string.Empty,
                        Quantity = item.Quantity,
                        BasePrice = item.BasePrice,
                        OptionPrice = item.OptionPrice,
                        Image = item.Image,
                        LineKey = item.LineKey,
                        BuildGroupKey = item.BuildGroupKey,
                        PcBuildId = item.PcBuildId,
                        BuildName = item.BuildName,
                        IsPcBuildItem = item.IsPcBuildItem,
                        ComponentType = item.ComponentType,
                        CreatedAt = now,
                        UpdatedAt = now,
                        SelectedOptions = item.SelectedOptions
                            .OrderBy(option => option.OptionGroupId)
                            .ThenBy(option => option.OptionValueId)
                            .Select(option => new UserCartItemOptionModel
                            {
                                OptionGroupId = option.OptionGroupId,
                                OptionValueId = option.OptionValueId,
                                GroupName = option.GroupName ?? string.Empty,
                                ValueName = option.ValueName ?? string.Empty,
                                AdditionalPrice = option.AdditionalPrice
                            })
                            .ToList()
                    }).ToList();

                    _dataContext.UserCartItems.AddRange(entities);
                    await _dataContext.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static List<CartItemModel> MergeCarts(IEnumerable<CartItemModel> existingCart, IEnumerable<CartItemModel> incomingCart)
        {
            var mergedCart = NormalizeCartItems(existingCart);
            foreach (var sessionItem in NormalizeCartItems(incomingCart))
            {
                var existingItem = mergedCart.FirstOrDefault(x => string.Equals(x.LineKey, sessionItem.LineKey, StringComparison.Ordinal));
                if (existingItem == null)
                {
                    mergedCart.Add(CloneCartItem(sessionItem));
                    continue;
                }

                existingItem.Quantity += sessionItem.Quantity;
            }

            return mergedCart;
        }

        private static List<CartItemModel> NormalizeCartItems(IEnumerable<CartItemModel>? cartItems)
        {
            var normalized = new List<CartItemModel>();
            var lineLookup = new Dictionary<string, CartItemModel>(StringComparer.Ordinal);

            foreach (var sourceItem in cartItems ?? Enumerable.Empty<CartItemModel>())
            {
                if (sourceItem == null || sourceItem.ProductId <= 0 || sourceItem.Quantity <= 0)
                {
                    continue;
                }

                var clonedItem = CloneCartItem(sourceItem);
                if (lineLookup.TryGetValue(clonedItem.LineKey, out var existingItem))
                {
                    existingItem.Quantity += clonedItem.Quantity;
                    continue;
                }

                lineLookup[clonedItem.LineKey] = clonedItem;
                normalized.Add(clonedItem);
            }

            return normalized;
        }

        private static CartItemModel CloneCartItem(CartItemModel source)
        {
            return new CartItemModel
            {
                ProductId = source.ProductId,
                ProductName = source.ProductName ?? string.Empty,
                Quantity = source.Quantity,
                BasePrice = source.BasePrice,
                OptionPrice = source.OptionPrice,
                Image = source.Image,
                BuildGroupKey = source.BuildGroupKey,
                PcBuildId = source.PcBuildId,
                BuildName = source.BuildName,
                IsPcBuildItem = source.IsPcBuildItem,
                ComponentType = source.ComponentType,
                SelectedOptions = (source.SelectedOptions ?? new List<CartItemOptionModel>())
                    .OrderBy(option => option.OptionGroupId)
                    .ThenBy(option => option.OptionValueId)
                    .Select(option => new CartItemOptionModel
                    {
                        OptionGroupId = option.OptionGroupId,
                        OptionValueId = option.OptionValueId,
                        GroupName = option.GroupName ?? string.Empty,
                        ValueName = option.ValueName ?? string.Empty,
                        AdditionalPrice = option.AdditionalPrice
                    })
                    .ToList()
            };
        }

        private static string? ResolveUserId(HttpContext httpContext, string? userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId.Trim();
            }

            return httpContext.User.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;
        }

        private async Task<List<CartItemModel>> FilterVisibleCartItemsAsync(IEnumerable<CartItemModel> cartItems)
        {
            var normalizedCart = NormalizeCartItems(cartItems);
            if (normalizedCart.Count == 0)
            {
                return normalizedCart;
            }

            var productIds = normalizedCart
                .Select(x => (int)x.ProductId)
                .Distinct()
                .ToList();

            var visibleProductIds = await _dataContext.Products
                .AsNoTracking()
                .WhereVisibleOnStorefront(_dataContext)
                .Where(x => productIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var visibleIdSet = visibleProductIds.ToHashSet();

            return normalizedCart
                .Where(x => visibleIdSet.Contains((int)x.ProductId))
                .ToList();
        }
    }
}
