using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Eshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Eshop.Controllers
{
    public class CartController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly UserManager<AppUserModel> _userManager;
        private readonly IInventoryService _inventoryService;


        public CartController(
                 DataContext dataContext,
                 UserManager<AppUserModel> userManager,
                 IInventoryService inventoryService)
        {
            _dataContext = dataContext;
            _userManager = userManager;
            _inventoryService = inventoryService;
        }


        public async Task<IActionResult> Index()
        {
            List<CartItemModel> cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            var shippingPriceCookie = Request.Cookies["ShippingPrice"];
            decimal shippingPrice = 0;

            if (!string.IsNullOrEmpty(shippingPriceCookie))
            {
                try
                {
                    shippingPrice = JsonConvert.DeserializeObject<decimal>(shippingPriceCookie);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error decoding shipping price cookie: {ex.Message}");
                }
            }

            decimal subTotal = cartItems.Sum(x => x.Total);
            decimal discountAmount = 0;
            string? couponCode = null;

            var appliedCoupon = HttpContext.Session.GetJson<AppliedCouponModel>("Coupon");
            if (appliedCoupon != null)
            {
                var coupon = await _dataContext.Coupons.FirstOrDefaultAsync(x => x.Id == appliedCoupon.CouponId);

                if (coupon != null)
                {
                    var now = DateTime.Now;

                    bool isValid =
                        coupon.Status == 1 &&
                        coupon.Quantity > 0 &&
                        coupon.DateStart <= now &&
                        coupon.DateEnd >= now &&
                        (!coupon.MinOrderAmount.HasValue || subTotal >= coupon.MinOrderAmount.Value);

                    if (isValid)
                    {
                        discountAmount = CalculateDiscount(coupon, subTotal);
                        couponCode = coupon.NameCode;
                    }
                    else
                    {
                        HttpContext.Session.Remove("Coupon");
                    }
                }
                else
                {
                    HttpContext.Session.Remove("Coupon");
                }
            }

            decimal finalTotal = subTotal - discountAmount + shippingPrice;
            if (finalTotal < 0) finalTotal = 0;

            CartItemViewModel cartItemViewModel = new()
            {
                CartItems = cartItems,
                GrandTotal = subTotal,
                ShippingPrice = shippingPrice,
                DiscountAmount = discountAmount,
                CouponCode = couponCode,
                FinalTotal = finalTotal
            };

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
            return View("~/Views/Checkout/Index.cshtml");
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
                .Include(p => p.OptionGroups)
                    .ThenInclude(g => g.OptionValues)
                .FirstOrDefaultAsync(p => p.Id == model.ProductId);


            if (product == null)
            {
                TempData["Error"] = "Sản phẩm không tồn tại.";
                return RedirectToAction("Index");
            }

            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            var newCartItem = new CartItemModel(product)
            {
                Quantity = model.Quantity > 0 ? model.Quantity : 1
            };

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

            var existingItem = cart.FirstOrDefault(x =>
                x.ProductId == newCartItem.ProductId &&
                IsSameOptions(x.SelectedOptions, newCartItem.SelectedOptions));

            if (existingItem == null)
            {
                cart.Add(newCartItem);
                TempData["Success"] = "Đã thêm sản phẩm vào giỏ hàng.";
            }
            else
            {
                int newQuantity = existingItem.Quantity + newCartItem.Quantity;

                if (newQuantity > availableStock)
                {
                    existingItem.Quantity = availableStock;
                    TempData["Error"] = "Không thể thêm quá số lượng tồn kho.";
                }
                else
                {
                    existingItem.Quantity = newQuantity;
                    TempData["Success"] = "Đã thêm sản phẩm vào giỏ hàng.";
                }
            }

            HttpContext.Session.SetJson("Cart", cart);

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Decrease(int id)
        {
            await ReleaseActiveReservationIfAnyAsync("Giỏ hàng thay đổi: giảm số lượng.");

            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            CartItemModel? cartItem = cart.FirstOrDefault(x => x.ProductId == id);

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
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }

            TempData["Success"] = "Cập nhật giỏ hàng thành công.";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Increase(int id)
        {
            await ReleaseActiveReservationIfAnyAsync("Giỏ hàng thay đổi: tăng số lượng.");

            ProductModel? product = await _dataContext.Products.FirstOrDefaultAsync(x => x.Id == id);
            if (product == null)
            {
                TempData["Error"] = "Sản phẩm không tồn tại.";
                return RedirectToAction("Index");
            }

            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            CartItemModel? cartItem = cart.FirstOrDefault(x => x.ProductId == id);


            if (cartItem == null)
            {
                TempData["Error"] = "Sản phẩm không tồn tại trong giỏ hàng.";
                return RedirectToAction("Index");
            }

            var availableStock = await GetAvailableStockAsync(id);

            if (cartItem.Quantity < availableStock)
            {
                cartItem.Quantity += 1;
                TempData["Success"] = "Cập nhật giỏ hàng thành công.";
            }
            else
            {
                TempData["Error"] = "Không đủ tồn kho để tăng thêm.";
            }

            HttpContext.Session.SetJson("Cart", cart);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Remove(int id)
        {
            await ReleaseActiveReservationIfAnyAsync("Giỏ hàng thay đổi: xóa sản phẩm.");

            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            cart.RemoveAll(x => x.ProductId == id);

            if (cart.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }

            TempData["Success"] = "Đã xóa sản phẩm khỏi giỏ hàng.";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Clear()
        {
            await ReleaseActiveReservationIfAnyAsync("Giỏ hàng thay đổi: xóa toàn bộ.");

            HttpContext.Session.Remove("Cart");
            TempData["Success"] = "Đã xóa toàn bộ giỏ hàng.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> GetShipping(ShippingModel shippingModel, string phuong, string tinh)
        {
            var exitstingShipping = await _dataContext.Shippings
                .FirstOrDefaultAsync(s => s.City == tinh && s.Ward == phuong);

            decimal shippingPrice = 0;

            if (exitstingShipping != null)
            {
                shippingPrice = exitstingShipping.ShippingCost;
            }
            else
            {
                shippingPrice = 50000;
            }

            var shippingPriceJson = JsonConvert.SerializeObject(shippingPrice);

            try
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(30),
                    Secure = true
                };

                Response.Cookies.Append("ShippingPrice", shippingPriceJson, cookieOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding shipping price cookie: {ex.Message}");
            }

            return Json(new { shippingPrice });
        }

        [HttpGet]
        public IActionResult DeleteShippingCookie()
        {
            try
            {
                Response.Cookies.Delete("ShippingPrice");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting shipping price cookie: {ex.Message}");
            }

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

            var cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

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

        [HttpGet]
        public IActionResult RemoveCoupon()
        {
            HttpContext.Session.Remove("Coupon");
            TempData["Success"] = "Đã bỏ mã giảm giá.";
            return RedirectToAction("Index");
        }

        private decimal CalculateDiscount(CouponModel coupon, decimal subTotal)
        {
            decimal discountAmount = 0;

            if (coupon == null || subTotal <= 0)
                return 0;

            if (coupon.DiscountType == 1)
            {
                discountAmount = subTotal * coupon.Discount / 100;
            }
            else if (coupon.DiscountType == 2)
            {
                discountAmount = coupon.Discount;
            }

            if (discountAmount > subTotal)
            {
                discountAmount = subTotal;
            }

            return discountAmount;
        }

        private bool IsSameOptions(List<CartItemOptionModel> oldOptions, List<CartItemOptionModel> newOptions)
        {
            if (oldOptions.Count != newOptions.Count) return false;

            var oldList = oldOptions
                .OrderBy(x => x.OptionGroupId)
                .ThenBy(x => x.OptionValueId)
                .Select(x => $"{x.OptionGroupId}-{x.OptionValueId}");

            var newList = newOptions
                .OrderBy(x => x.OptionGroupId)
                .ThenBy(x => x.OptionValueId)
                .Select(x => $"{x.OptionGroupId}-{x.OptionValueId}");

            return oldList.SequenceEqual(newList);
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
        }


    }
}