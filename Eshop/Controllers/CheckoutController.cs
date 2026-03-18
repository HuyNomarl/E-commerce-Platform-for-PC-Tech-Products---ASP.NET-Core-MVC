using Eshop.Models.ViewModel;
using Eshop.Services;
using Microsoft.AspNetCore.Mvc;

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

            try
            {
                var orderCode = await _orderService.CreateOrderFromSessionAsync(HttpContext, User, model);

                if (string.IsNullOrEmpty(orderCode))
                {
                    TempData["Error"] = "Không thể tạo đơn hàng.";
                    return RedirectToAction("Index", "Cart");
                }

                TempData["Success"] = $"Đặt hàng thành công, mã đơn: {orderCode}";
                return RedirectToAction("Index", "Home");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index", "Cart");
            }
            catch
            {
                TempData["Error"] = "Có lỗi xảy ra trong quá trình đặt hàng.";
                return RedirectToAction("Index", "Cart");
            }
        }
    }
}