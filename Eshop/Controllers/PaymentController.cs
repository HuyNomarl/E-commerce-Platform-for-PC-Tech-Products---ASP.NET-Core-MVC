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
        private readonly IInventoryService _inventoryService;
        public PaymentController(
            IMomoService momoService,
            IVnPayService vnPayService,
            IOrderService orderService,
            IInventoryService inventoryService)
        {
            _momoService = momoService;
            _vnPayService = vnPayService;
            _orderService = orderService;
            _inventoryService = inventoryService;
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePaymentUrlVnpay(PaymentInformationModel paymentModel, CheckoutInputViewModel checkoutModel)
        {
            if (paymentModel == null || paymentModel.Amount <= 0)
            {
                TempData["error"] = "Dữ liệu thanh toán VNPAY không hợp lệ.";
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

            try
            {
                checkoutModel.PaymentMethod = "VNPAY";
                var reservationCode = await _inventoryService.ReserveCartAsync(HttpContext, User, "VNPAY");

                HttpContext.Session.SetJson("CheckoutInfo", checkoutModel);
                HttpContext.Session.SetString("ActiveReservationCode", reservationCode);

                var url = _vnPayService.CreatePaymentUrl(paymentModel, HttpContext);
                return Redirect(url);
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;
                return RedirectToAction("Index", "Cart");
            }
        }


        [HttpGet]
        public async Task<IActionResult> PaymentCallbackVnpay()
        {
            var response = _vnPayService.PaymentExecute(Request.Query);
            var reservationCode = HttpContext.Session.GetString("ActiveReservationCode");

            if (response == null)
            {
                if (!string.IsNullOrWhiteSpace(reservationCode))
                    await _inventoryService.ReleaseReservationAsync(reservationCode, User.Identity?.Name, "VNPAY không trả về phản hồi.");

                TempData["error"] = "Không nhận được phản hồi từ VNPAY.";
                return RedirectToAction("Index", "Cart");
            }

            if (!response.Success)
            {
                if (!string.IsNullOrWhiteSpace(reservationCode))
                    await _inventoryService.ReleaseReservationAsync(reservationCode, User.Identity?.Name, $"VNPAY fail: {response.VnPayResponseCode}");

                HttpContext.Session.Remove("CheckoutInfo");
                HttpContext.Session.Remove("ActiveReservationCode");

                TempData["error"] = $"Thanh toán VNPAY thất bại. Mã lỗi: {response.VnPayResponseCode}";
                return RedirectToAction("Index", "Cart");
            }

            var checkoutInfo = HttpContext.Session.GetJson<CheckoutInputViewModel>("CheckoutInfo");
            if (checkoutInfo == null || string.IsNullOrWhiteSpace(reservationCode))
            {
                TempData["error"] = "Không tìm thấy thông tin checkout hoặc reservation.";
                return RedirectToAction("Index", "Cart");
            }

            try
            {
                var orderCode = await _orderService.CreateOrderFromReservationAsync(HttpContext, User, checkoutInfo, reservationCode);

                if (string.IsNullOrEmpty(orderCode))
                {
                    await _inventoryService.ReleaseReservationAsync(reservationCode, User.Identity?.Name, "Tạo đơn sau VNPAY thất bại.");
                    TempData["error"] = "Thanh toán thành công nhưng không thể tạo đơn hàng.";
                    return RedirectToAction("Index", "Cart");
                }

                HttpContext.Session.Remove("CheckoutInfo");
                HttpContext.Session.Remove("ActiveReservationCode");

                TempData["success"] = $"Thanh toán VNPAY thành công. Mã đơn: {orderCode}";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                await _inventoryService.ReleaseReservationAsync(reservationCode, User.Identity?.Name, "Rollback sau lỗi tạo đơn VNPAY.");
                TempData["error"] = ex.Message;
                return RedirectToAction("Index", "Cart");
            }
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