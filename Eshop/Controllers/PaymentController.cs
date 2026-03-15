using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Models.VNPay;
using Eshop.Repository;
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
        [ValidateAntiForgeryToken]
        public IActionResult CreatePaymentUrlVnpay(PaymentInformationModel paymentModel, CheckoutInputViewModel checkoutModel)
        {
            if (paymentModel == null)
            {
                TempData["error"] = "Dữ liệu thanh toán VNPAY không hợp lệ.";
                return RedirectToAction("Index", "Cart");
            }

            if (paymentModel.Amount <= 0)
            {
                TempData["error"] = "Số tiền thanh toán không hợp lệ.";
                return RedirectToAction("Index", "Cart");
            }

            if (string.IsNullOrWhiteSpace(checkoutModel.FullName) ||
                string.IsNullOrWhiteSpace(checkoutModel.Phone) ||
                string.IsNullOrWhiteSpace(checkoutModel.Email) ||
                string.IsNullOrWhiteSpace(checkoutModel.Address) ||
                string.IsNullOrWhiteSpace(checkoutModel.tinh) ||
                string.IsNullOrWhiteSpace(checkoutModel.phuong))
            {
                TempData["error"] = "Vui lòng nhập đầy đủ thông tin đặt hàng.";
                return RedirectToAction("Index", "Cart");
            }

            HttpContext.Session.SetJson("CheckoutInfo", checkoutModel);

            var url = _vnPayService.CreatePaymentUrl(paymentModel, HttpContext);
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

            var checkoutInfo = HttpContext.Session.GetJson<CheckoutInputViewModel>("CheckoutInfo");
            if (checkoutInfo == null)
            {
                TempData["error"] = "Không tìm thấy thông tin đặt hàng trong phiên làm việc.";
                return RedirectToAction("Index", "Cart");
            }

            var orderCode = await _orderService.CreateOrderFromSessionAsync(HttpContext, User, checkoutInfo);

            if (string.IsNullOrEmpty(orderCode))
            {
                TempData["error"] = "Thanh toán thành công nhưng không thể tạo đơn hàng.";
                return RedirectToAction("Index", "Cart");
            }

            HttpContext.Session.Remove("CheckoutInfo");

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