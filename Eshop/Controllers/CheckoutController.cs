using Eshop.Areas.Admin.Repository;
using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Repository;
using Eshop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;

namespace Eshop.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly IOrderService _orderService;

        public CheckoutController(IOrderService orderService)
        {
            _orderService = orderService;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutInputViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin đặt hàng.";
                return RedirectToAction("Index", "Cart");
            }

            var orderCode = await _orderService.CreateOrderFromSessionAsync(HttpContext, User, model);

            if (string.IsNullOrEmpty(orderCode))
            {
                TempData["Error"] = "Không thể tạo đơn hàng.";
                return RedirectToAction("Index", "Cart");
            }

            TempData["Success"] = $"Đặt hàng thành công, mã đơn: {orderCode}";
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