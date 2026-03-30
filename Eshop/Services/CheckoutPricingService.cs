using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Services
{
    public class CheckoutPricingService : ICheckoutPricingService
    {
        private const string ShippingSessionKey = "CheckoutShippingSelection";

        private readonly DataContext _dataContext;

        public CheckoutPricingService(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<CheckoutPricingSummaryViewModel> BuildSummaryAsync(HttpContext httpContext, CheckoutInputViewModel? checkoutModel = null)
        {
            var cartItems = httpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            var subTotal = cartItems.Sum(x => x.Total);
            var shippingSelection = await ResolveShippingSelectionAsync(httpContext, checkoutModel);
            var shippingCost = await ResolveShippingCostAsync(shippingSelection);

            decimal discountAmount = 0m;
            int? couponId = null;
            string? couponCode = null;

            var appliedCoupon = httpContext.Session.GetJson<AppliedCouponModel>("Coupon");
            if (appliedCoupon != null)
            {
                var coupon = await _dataContext.Coupons
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == appliedCoupon.CouponId);

                if (coupon != null && IsCouponValid(coupon, subTotal))
                {
                    discountAmount = CalculateDiscount(coupon, subTotal);
                    couponId = coupon.Id;
                    couponCode = coupon.NameCode;
                }
                else
                {
                    httpContext.Session.Remove("Coupon");
                }
            }

            var totalAmount = subTotal - discountAmount + shippingCost;
            if (totalAmount < 0)
            {
                totalAmount = 0;
            }

            return new CheckoutPricingSummaryViewModel
            {
                CartItems = cartItems,
                SubTotal = subTotal,
                ShippingCost = shippingCost,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                CouponCode = couponCode,
                CouponId = couponId,
                ShippingSelection = shippingSelection
            };
        }

        public Task<CheckoutShippingSelectionViewModel?> GetStoredShippingSelectionAsync(HttpContext httpContext)
        {
            var selection = httpContext.Session.GetJson<CheckoutShippingSelectionViewModel>(ShippingSessionKey);
            return Task.FromResult(selection);
        }

        public void SaveShippingSelection(HttpContext httpContext, CheckoutShippingSelectionViewModel selection)
        {
            httpContext.Session.SetJson(ShippingSessionKey, new CheckoutShippingSelectionViewModel
            {
                ProvinceCode = (selection.ProvinceCode ?? string.Empty).Trim(),
                WardCode = (selection.WardCode ?? string.Empty).Trim(),
                ProvinceName = selection.ProvinceName?.Trim(),
                DistrictName = selection.DistrictName?.Trim(),
                WardName = selection.WardName?.Trim()
            });
        }

        public void ClearShippingSelection(HttpContext httpContext)
        {
            httpContext.Session.Remove(ShippingSessionKey);
        }

        private async Task<CheckoutShippingSelectionViewModel?> ResolveShippingSelectionAsync(HttpContext httpContext, CheckoutInputViewModel? checkoutModel)
        {
            if (checkoutModel != null &&
                !string.IsNullOrWhiteSpace(checkoutModel.ProvinceCode) &&
                !string.IsNullOrWhiteSpace(checkoutModel.WardCode))
            {
                var selection = new CheckoutShippingSelectionViewModel
                {
                    ProvinceCode = checkoutModel.ProvinceCode.Trim(),
                    WardCode = checkoutModel.WardCode.Trim(),
                    ProvinceName = checkoutModel.tinh?.Trim(),
                    DistrictName = checkoutModel.quan?.Trim(),
                    WardName = checkoutModel.phuong?.Trim()
                };

                await HydrateShippingNamesAsync(selection);
                return selection;
            }

            var storedSelection = await GetStoredShippingSelectionAsync(httpContext);
            if (storedSelection != null)
            {
                await HydrateShippingNamesAsync(storedSelection);
            }

            return storedSelection;
        }

        private async Task HydrateShippingNamesAsync(CheckoutShippingSelectionViewModel selection)
        {
            if (selection == null ||
                string.IsNullOrWhiteSpace(selection.ProvinceCode) ||
                string.IsNullOrWhiteSpace(selection.WardCode))
            {
                return;
            }

            var shipping = await _dataContext.Shippings
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.CityCode == selection.ProvinceCode &&
                    x.WardCode == selection.WardCode);

            if (shipping == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(selection.ProvinceName))
            {
                selection.ProvinceName = shipping.City;
            }

            if (string.IsNullOrWhiteSpace(selection.DistrictName))
            {
                selection.DistrictName = shipping.District;
            }

            if (string.IsNullOrWhiteSpace(selection.WardName))
            {
                selection.WardName = shipping.Ward;
            }
        }

        private async Task<decimal> ResolveShippingCostAsync(CheckoutShippingSelectionViewModel? selection)
        {
            if (selection == null ||
                string.IsNullOrWhiteSpace(selection.ProvinceCode) ||
                string.IsNullOrWhiteSpace(selection.WardCode))
            {
                return 0m;
            }

            var shipping = await _dataContext.Shippings
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.CityCode == selection.ProvinceCode &&
                    x.WardCode == selection.WardCode);

            if (shipping != null)
            {
                return shipping.ShippingCost < 0 ? 0m : shipping.ShippingCost;
            }

            return 50000m;
        }

        private static bool IsCouponValid(CouponModel coupon, decimal subTotal)
        {
            var now = DateTime.Now;

            return coupon.Status == 1 &&
                   coupon.Quantity > 0 &&
                   coupon.DateStart <= now &&
                   coupon.DateEnd >= now &&
                   (!coupon.MinOrderAmount.HasValue || subTotal >= coupon.MinOrderAmount.Value);
        }

        private static decimal CalculateDiscount(CouponModel coupon, decimal subTotal)
        {
            if (subTotal <= 0)
            {
                return 0;
            }

            decimal discountAmount = coupon.DiscountType switch
            {
                1 => subTotal * coupon.Discount / 100,
                2 => coupon.Discount,
                _ => 0
            };

            if (coupon.MaxDiscountAmount.HasValue && discountAmount > coupon.MaxDiscountAmount.Value)
            {
                discountAmount = coupon.MaxDiscountAmount.Value;
            }

            return discountAmount > subTotal ? subTotal : discountAmount;
        }
    }
}
