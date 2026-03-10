using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Eshop.Controllers
{
    public class CartController : Controller
    {
        private readonly DataContext _dataContext;
        public CartController(DataContext dataContext)
        {
            _dataContext = dataContext;
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

            decimal subTotal = cartItems.Sum(x => x.Quantity * x.Price);
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

            return View(cartItemViewModel);
        }
        public IActionResult Checkout()
        {
            return View("~/Views/Checkout/Index.cshtml");
        }


        public async Task<IActionResult> Add(int id)
        {
            ProductModel product = await _dataContext.Products.FindAsync(id);
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            CartItemModel cartItem = cart.Where(x => x.ProductId == id).FirstOrDefault();
            if (cartItem == null)
            {
                cart.Add(new CartItemModel(product));
            }
            else
            {
                cartItem.Quantity += 1;
            }
            HttpContext.Session.SetJson("Cart", cart);

            TempData["Success"] = "Product added to cart successfully!";

            return Redirect(Request.Headers["Referer"].ToString());
        }

        public async Task<IActionResult> Decrease(int id)
        {
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            CartItemModel cartItem = cart.Where(x => x.ProductId == id).FirstOrDefault();

            if (cartItem.Quantity > 1)
            {
                cartItem.Quantity -= 1;
            }
            else
            {
                cart.RemoveAll(x => x.ProductId == id);
            }

            if (cart.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }
            TempData["Success"] = "Cart updated (-) successfully!";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Increase(int id)
        {
            ProductModel product = await _dataContext.Products.Where(x => x.Id == id).FirstOrDefaultAsync();
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();

            CartItemModel cartItem = cart.Where(x => x.ProductId == id).FirstOrDefault();

            if (cartItem.Quantity >= 1 && product.Quantity > cartItem.Quantity)
            {
                ++cartItem.Quantity;
            }
            else
            {
                cartItem.Quantity = product.Quantity;
                //cart.RemoveAll(x => x.ProductId == id);
                TempData["Error"] = "Cannot increase quantity. Not enough stock available.";
            }

            if (cart.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }
            TempData["Success"] = "Cart updated (+) successfully!";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Remove(int id)
        {
            List<CartItemModel> cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart");
            cart.RemoveAll(x => x.ProductId == id);
            if (cart.Count == 0)
            {
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                HttpContext.Session.SetJson("Cart", cart);
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Clear()
        {
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index");
        }
        [HttpPost]
        public async Task<IActionResult> GetShipping(ShippingModel shippingModel, string phuong, string tinh)
        {
            var exitstingShipping = await _dataContext.Shippings.FirstOrDefaultAsync(s => s.City == tinh && s.Ward == phuong);

            decimal shippingPrice = 0;

            if (exitstingShipping != null)
            {
                shippingPrice = exitstingShipping.ShippingCost;
            }
            else
            {
                shippingPrice = 50000; // Default shipping cost if not found
            }
            var shippingPriceJson = JsonConvert.SerializeObject(shippingPrice);

            try
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(30),
                    Secure = true // using HTTPS
                };

                Response.Cookies.Append("ShippingPrice", shippingPriceJson, cookieOptions);
            }
            catch (Exception ex)
            {
                // Log lỗi nếu thêm cookie thất bại
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
                // Log lỗi nếu xóa cookie thất bại
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

            decimal subTotal = cartItems.Sum(x => x.Quantity * x.Price);

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
    }
}
