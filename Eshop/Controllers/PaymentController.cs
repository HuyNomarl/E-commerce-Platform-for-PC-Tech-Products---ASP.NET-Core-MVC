using Eshop.Models;
using Eshop.Models.VNPay;
using Eshop.Services;
using Eshop.Services.Momo;
using Eshop.Services.VNPay;
using Microsoft.AspNetCore.Mvc;

namespace Eshop.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IMomoService _momoService;
        private readonly IVnPayService _vnPayService;
        private readonly IOrderService _orderService;

        public PaymentController(
      IMomoService momoService,
      IVnPayService vnPayService,
      IOrderService orderService)
        {
            _momoService = momoService;
            _vnPayService = vnPayService;
            _orderService = orderService;
        }

        [HttpPost]
        public IActionResult CreatePaymentUrlVnpay(PaymentInformationModel model)
        {
            if (model == null)
            {
                TempData["error"] = "Dữ liệu thanh toán VNPAY không hợp lệ.";
                return RedirectToAction("Index", "Cart");
            }

            if (model.Amount <= 0)
            {
                TempData["error"] = "Số tiền thanh toán không hợp lệ.";
                return RedirectToAction("Index", "Cart");
            }

            var url = _vnPayService.CreatePaymentUrl(model, HttpContext);
            return Redirect(url);
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallbackVnpay()
        {
            var response = _vnPayService.PaymentExecute(Request.Query);

            if (response == null)
            {
                TempData["error"] = "Không nhận được phản hồi từ VNPAY.";
                return RedirectToAction("Index", "Cart");
            }

            if (!response.Success)
            {
                TempData["error"] = $"Thanh toán VNPAY thất bại. Mã lỗi: {response.VnPayResponseCode}";
                return RedirectToAction("Index", "Cart");
            }

            var orderCode = await _orderService.CreateOrderFromSessionAsync(HttpContext, User);

            if (string.IsNullOrEmpty(orderCode))
            {
                TempData["error"] = "Thanh toán thành công nhưng không thể tạo đơn hàng.";
                return RedirectToAction("Index", "Cart");
            }

            TempData["success"] = $"Thanh toán VNPAY thành công. Mã đơn: {orderCode}";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> CreatePaymentMomo(OrderInfoModel model)
        {
            var response = await _momoService.CreatePaymentAsync(model);

            if (response == null || string.IsNullOrWhiteSpace(response.PayUrl))
            {
                TempData["error"] = $"MoMo không trả về payUrl. Message: {response?.Message}, ResultCode: {response?.ResultCode}";
                return RedirectToAction("Index", "Cart");
            }

            return Redirect(response.PayUrl);
        }

        [HttpGet]
        public IActionResult PaymentCallback()
        {
            var response = _momoService.PaymentExecuteAsync(HttpContext.Request.Query);
            return View(response);
        }

        [HttpPost]
        public IActionResult MomoNotify()
        {
            return Ok();
        }
    }
}