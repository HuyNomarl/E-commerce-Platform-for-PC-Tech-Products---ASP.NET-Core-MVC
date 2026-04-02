using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Eshop.Services;
using Eshop.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Controllers
{
    public class CartController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly UserManager<AppUserModel> _userManager;
        private readonly IInventoryService _inventoryService;
        private readonly ICheckoutPricingService _checkoutPricingService;
        private readonly ICartService _cartService;


        public CartController(
                 DataContext dataContext,
                 UserManager<AppUserModel> userManager,
                 IInventoryService inventoryService,
                 ICheckoutPricingService checkoutPricingService,
                 ICartService cartService)
        {
            _dataContext = dataContext;
            _userManager = userManager;
            _inventoryService = inventoryService;
            _checkoutPricingService = checkoutPricingService;
            _cartService = cartService;
        }


        public async Task<IActionResult> Index()
        {
            var pricingSummary = await _checkoutPricingService.BuildSummaryAsync(HttpContext);

            CartItemViewModel cartItemViewModel = new()
            {
                CartItems = pricingSummary.CartItems,
                GrandTotal = pricingSummary.SubTotal,
                ShippingPrice = pricingSummary.ShippingCost,
                DiscountAmount = pricingSummary.DiscountAmount,
                CouponCode = pricingSummary.CouponCode,
                FinalTotal = pricingSummary.TotalAmount
            };

            ViewBag.SelectedProvinceCode = pricingSummary.ShippingSelection?.ProvinceCode ?? "";
            ViewBag.SelectedWardCode = pricingSummary.ShippingSelection?.WardCode ?? "";
            ViewBag.SelectedProvinceName = pricingSummary.ShippingSelection?.ProvinceName ?? "";
            ViewBag.SelectedWardName = pricingSummary.ShippingSelection?.WardName ?? "";

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var currentUser = await _userManager.GetUserAsync(User);

                if (currentUser != null)
                {
                    ViewBag.FullName = currentUser.UserName ?? "";
                    ViewBag.Phone = currentUser.PhoneNumber ?? "";
                    ViewBag.Email = currentUser.Email ?? "";
                    ViewBag.Address = currentUser.Address ?? "";
                }
            }

            return View(cartItemViewModel);
        }

        public IActionResult Checkout()
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(ProductDetailViewModel model)
        {
            await ReleaseActiveReservationIfAnyAsync("Giỏ hàng thay đổi: thêm sản phẩm.");

            if (model.SelectedOptions == null)
            {
                model.SelectedOptions = new List<SelectedOptionViewModel>();
            }

            var product = await _dataContext.Products
                .Include(p => p.ProductImages)
                .Include(p => p.OptionGroups)
                    .ThenInclude(g => g.OptionValues)
                .FirstOrDefaultAsync(p => p.Id == model.ProductId);


            if (product == null)
            {
                TempData["Error"] = "Sản phẩm không tồn tại.";
                return RedirectToAction("Index");
            }

            List<CartItemModel> cart = await _cartService.GetCartAsync(HttpContext);

            var newCartItem = new CartItemModel(product)
            {
                Quantity = model.Quantity > 0 ? model.Quantity : 1
            };
            newCartItem.Image = ProductImageHelper.ResolveProductImage(product);

            var availableStock = await GetAvailableStockAsync(product.Id);

            if (newCartItem.Quantity > availableStock)
            {
                TempData["Error"] = $"Sản phẩm chỉ còn {availableStock} trong kho.";
                return RedirectToAction("Details", "Product", new { id = product.Id });
            }


            foreach (var selected in model.SelectedOptions)
            {
                var group = product.OptionGroups.FirstOrDefault(g => g.Id == selected.GroupId);
                if (group == null) continue;

                var value = group.OptionValues.FirstOrDefault(v => v.Id == selected.ValueId);
                if (value == null) continue;

                newCartItem.SelectedOptions.Add(new CartItemOptionModel
                {
                    OptionGroupId = group.Id,
                    OptionValueId = value.Id,
                    GroupName = group.Name,
                    ValueName = value.Value,
                    AdditionalPrice = value.AdditionalPrice
                });

                newCartItem.OptionPrice += value.AdditionalPrice;
            }

            var existingItem = cart.FirstOrDefault(x => IsSameCartLine(x, newCartItem));
            var quantityInOtherLines = cart
                .Where(x => x.ProductId == newCartItem.ProductId && !IsSameCartLine(x, newCartItem))
                .Sum(x => x.Quantity);

            if (existingItem == null)
            {
                if (quantityInOtherLines + newCartItem.Quantity > availableStock)
                {
                    TempData["Error"] = $"Tổng số lượng sản phẩm này trong giỏ đã chạm mức tồn kho ({availableStock}).";
                    return RedirectToAction("Details", "Product", new { id = product.Id });
                }

                cart.Add(newCartItem);
                TempData["Success"] = "Đã thêm sản phẩm vào giỏ hàng.";
            }
            else
            {
                int newQuantity = existingItem.Quantity + newCartItem.Quantity;
                int maxAllowedForCurrentLine = Math.Max(0, availableStock - quantityInOtherLines);

                if (newQuantity > maxAllowedForCurrentLine)
                {
                    existingItem.Quantity = maxAllowedForCurrentLine;
                    if (existingItem.Quantity <= 0)
                    {
                        cart.Remove(existingItem);
                    }
                    TempData["Error"] = "Không thể thêm quá số lượng tồn kho.";
                }
                else
                {
                    existingItem.Quantity = newQuantity;
                    TempData["Success"] = "Đã thêm sản phẩm vào giỏ hàng.";
                }
            }

            await _cartService.SaveCartAsync(HttpContext, cart);

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decrease(string lineKey)
        {
            await ReleaseActiveReservationIfAnyAsync("Giỏ hàng thay đổi: giảm số lượng.");

            List<CartItemModel> cart = await _cartService.GetCartAsync(HttpContext);

            CartItemModel? cartItem = FindCartItem(cart, lineKey);

            if (cartItem == null)
            {
                TempData["Error"] = "Sản phẩm không tồn tại trong giỏ hàng.";
                return RedirectToAction("Index");
            }

            if (cartItem.Quantity > 1)
            {
                cartItem.Quantity -= 1;
            }
            else
            {
                cart.Remove(cartItem);
            }

            if (cart.Count == 0)
            {
                await _cartService.ClearCartAsync(HttpContext);
                HttpContext.Session.Remove("Coupon");
                _checkoutPricingService.ClearShippingSelection(HttpContext);
            }
            else
            {
                await _cartService.SaveCartAsync(HttpContext, cart);
            }

            TempData["Success"] = "Cập nhật giỏ hàng thành công.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Increase(string lineKey)
        {
            await ReleaseActiveReservationIfAnyAsync("Giỏ hàng thay đổi: tăng số lượng.");

            List<CartItemModel> cart = await _cartService.GetCartAsync(HttpContext);
            CartItemModel? cartItem = FindCartItem(cart, lineKey);

            if (cartItem == null)
            {
                TempData["Error"] = "Sản phẩm không tồn tại trong giỏ hàng.";
                return RedirectToAction("Index");
            }

            ProductModel? product = await _dataContext.Products.FirstOrDefaultAsync(x => x.Id == cartItem.ProductId);
            if (product == null)
            {
                TempData["Error"] = "Sản phẩm không tồn tại.";
                return RedirectToAction("Index");
            }

            var availableStock = await GetAvailableStockAsync(product.Id);
            var quantityInOtherLines = cart
                .Where(x => x.ProductId == cartItem.ProductId && x.LineKey != cartItem.LineKey)
                .Sum(x => x.Quantity);

            if (quantityInOtherLines + cartItem.Quantity < availableStock)
            {
                cartItem.Quantity += 1;
                TempData["Success"] = "Cập nhật giỏ hàng thành công.";
            }
            else
            {
                TempData["Error"] = "Không đủ tồn kho để tăng thêm.";
            }

            await _cartService.SaveCartAsync(HttpContext, cart);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(string lineKey)
        {
            await ReleaseActiveReservationIfAnyAsync("Giỏ hàng thay đổi: xóa sản phẩm.");

            List<CartItemModel> cart = await _cartService.GetCartAsync(HttpContext);

            var cartItem = FindCartItem(cart, lineKey);
            if (cartItem != null)
            {
                cart.Remove(cartItem);
            }

            if (cart.Count == 0)
            {
                await _cartService.ClearCartAsync(HttpContext);
                HttpContext.Session.Remove("Coupon");
                _checkoutPricingService.ClearShippingSelection(HttpContext);
            }
            else
            {
                await _cartService.SaveCartAsync(HttpContext, cart);
            }

            TempData["Success"] = "Đã xóa sản phẩm khỏi giỏ hàng.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            await ReleaseActiveReservationIfAnyAsync("Giỏ hàng thay đổi: xóa toàn bộ.");

            await _cartService.ClearCartAsync(HttpContext);
            HttpContext.Session.Remove("Coupon");
            _checkoutPricingService.ClearShippingSelection(HttpContext);
            TempData["Success"] = "Đã xóa toàn bộ giỏ hàng.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetShipping(string provinceCode, string wardCode, string provinceName, string wardName)
        {
            if (string.IsNullOrWhiteSpace(provinceCode) || provinceCode == "0" ||
                string.IsNullOrWhiteSpace(wardCode) || wardCode == "0")
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn đầy đủ tỉnh/thành và phường/xã." });
            }

            _checkoutPricingService.SaveShippingSelection(HttpContext, new CheckoutShippingSelectionViewModel
            {
                ProvinceCode = provinceCode,
                WardCode = wardCode,
                ProvinceName = provinceName,
                WardName = wardName
            });

            var pricingSummary = await _checkoutPricingService.BuildSummaryAsync(HttpContext);
            return Json(new
            {
                success = true,
                shippingPrice = pricingSummary.ShippingCost,
                totalAmount = pricingSummary.TotalAmount
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteShippingCookie()
        {
            _checkoutPricingService.ClearShippingSelection(HttpContext);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyCoupon(string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
            {
                TempData["Error"] = "Vui lòng nhập mã giảm giá.";
                return RedirectToAction("Index");
            }

            var cartItems = await _cartService.GetCartAsync(HttpContext);

            if (cartItems.Count == 0)
            {
                TempData["Error"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index");
            }

            decimal subTotal = cartItems.Sum(x => x.Total);

            var coupon = await _dataContext.Coupons
                .FirstOrDefaultAsync(x => x.NameCode == couponCode.Trim());

            if (coupon == null)
            {
                TempData["Error"] = "Mã giảm giá không tồn tại.";
                return RedirectToAction("Index");
            }

            var now = DateTime.Now;

            if (coupon.Status != 1)
            {
                TempData["Error"] = "Mã giảm giá hiện không hoạt động.";
                return RedirectToAction("Index");
            }

            if (coupon.Quantity <= 0)
            {
                TempData["Error"] = "Mã giảm giá đã hết lượt sử dụng.";
                return RedirectToAction("Index");
            }

            if (coupon.DateStart > now || coupon.DateEnd < now)
            {
                TempData["Error"] = "Mã giảm giá đã hết hạn hoặc chưa đến thời gian sử dụng.";
                return RedirectToAction("Index");
            }

            if (coupon.MinOrderAmount.HasValue && subTotal < coupon.MinOrderAmount.Value)
            {
                TempData["Error"] = $"Đơn hàng phải từ {coupon.MinOrderAmount.Value:N0} đ để áp dụng mã này.";
                return RedirectToAction("Index");
            }

            var appliedCoupon = new AppliedCouponModel
            {
                CouponId = coupon.Id,
                CouponCode = coupon.NameCode
            };

            HttpContext.Session.SetJson("Coupon", appliedCoupon);

            TempData["Success"] = $"Áp dụng mã {coupon.NameCode} thành công.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveCoupon()
        {
            HttpContext.Session.Remove("Coupon");
            TempData["Success"] = "Đã bỏ mã giảm giá.";
            return RedirectToAction("Index");
        }

        private static bool IsSameCartLine(CartItemModel left, CartItemModel right)
        {
            return string.Equals(left.LineKey, right.LineKey, StringComparison.Ordinal);
        }

        private static CartItemModel? FindCartItem(List<CartItemModel> cart, string? lineKey)
        {
            if (string.IsNullOrWhiteSpace(lineKey))
                return null;

            return cart.FirstOrDefault(x => x.LineKey == lineKey);
        }

        private async Task<int> GetAvailableStockAsync(int productId)
        {
            return await _inventoryService.GetAvailableStockAsync(productId);
        }
        private async Task ReleaseActiveReservationIfAnyAsync(string reason)
        {
            var reservationCode = HttpContext.Session.GetString("ActiveReservationCode");

            if (string.IsNullOrWhiteSpace(reservationCode))
                return;

            await _inventoryService.ReleaseReservationAsync(
                reservationCode,
                User.Identity?.Name,
                reason);

            HttpContext.Session.Remove("ActiveReservationCode");
            HttpContext.Session.Remove("CheckoutInfo");
            HttpContext.Session.Remove("PendingCheckoutState");
        }


    }
}
