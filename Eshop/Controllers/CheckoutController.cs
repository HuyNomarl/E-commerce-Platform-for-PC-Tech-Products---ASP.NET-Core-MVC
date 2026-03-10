using Eshop.Areas.Admin.Repository;
using Eshop.Models;
using Eshop.Repository;
using Eshop.Models.ViewModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Claims;

namespace Eshop.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly IEmailSender _emailSender;

        public CheckoutController(DataContext dataContext, IEmailSender emailSender)
        {
            _dataContext = dataContext;
            _emailSender = emailSender;
        }


        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");

            var cartItems = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            if (cartItems.Count == 0)
            {
                TempData["Error"] = "Giỏ hàng trống!";
                return RedirectToAction("Index", "Cart");
            }

            decimal subTotal = cartItems.Sum(x => x.Quantity * x.Price);

            decimal shippingCost = 0m;
            var shippingCookie = Request.Cookies["ShippingPrice"];

            if (!string.IsNullOrWhiteSpace(shippingCookie))
            {
                try
                {
                    shippingCost = JsonConvert.DeserializeObject<decimal>(shippingCookie);
                    if (shippingCost < 0) shippingCost = 0m;
                }
                catch
                {
                    shippingCost = 0m;
                }
            }

            decimal discountAmount = 0m;
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

                        coupon.Quantity -= 1;
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

            decimal totalAmount = subTotal - discountAmount + shippingCost;
            if (totalAmount < 0) totalAmount = 0;

            var orderCode = Guid.NewGuid().ToString("N");

            var order = new OrderModel
            {
                OrderCode = orderCode,
                UserName = userEmail,
                Status = 1,
                CreatedTime = DateTime.Now,

                SubTotal = subTotal,
                DiscountAmount = discountAmount,
                CouponCode = couponCode,
                ShippingCost = shippingCost,
                TotalAmount = totalAmount
            };

            _dataContext.Orders.Add(order);

            foreach (var cart in cartItems)
            {
                _dataContext.OrderDetails.Add(new OrderDetails
                {
                    UserName = userEmail,
                    OrderCode = orderCode,
                    ProductId = (int)cart.ProductId,
                    Price = cart.Price,
                    Quantity = cart.Quantity
                });
            }

            await _dataContext.SaveChangesAsync();

            var subject = $"Eshop - Đặt hàng thành công #{orderCode}";
            var body = $@"
        <h3>Cảm ơn bạn đã đặt hàng!</h3>
        <p>Mã đơn hàng: <b>{orderCode}</b></p>
        <p>Tạm tính: <b>{subTotal:N0} đ</b></p>
        <p>Giảm giá: <b>{discountAmount:N0} đ</b></p>
        <p>Phí ship: <b>{shippingCost:N0} đ</b></p>
        <p>Tổng thanh toán: <b>{totalAmount:N0} đ</b></p>
        <p>Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm}</p>
    ";

            try
            {
                await _emailSender.SendEmailAsync(userEmail, subject, body);
            }
            catch
            {
            }

            HttpContext.Session.Remove("Cart");
            HttpContext.Session.Remove("Coupon");
            Response.Cookies.Delete("ShippingPrice");

            TempData["Success"] = "Đặt hàng thành công, vui lòng chờ duyệt!";
            return RedirectToAction("Index", "Home");
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

            if (coupon.MaxDiscountAmount.HasValue && discountAmount > coupon.MaxDiscountAmount.Value)
            {
                discountAmount = coupon.MaxDiscountAmount.Value;
            }

            if (discountAmount > subTotal)
            {
                discountAmount = subTotal;
            }

            return discountAmount;
        }
    }
}