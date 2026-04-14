using Eshop.Constants;
using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Models.VNPay;
using Eshop.Repository;
using Eshop.Services;
using Eshop.Services.Momo;
using Eshop.Services.VNPay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Security.Claims;

namespace Eshop.Controllers
{
    public class PaymentController : Controller
    {
        private const string PendingCheckoutCacheKeyPrefix = "pending-checkout:";
        private const string PendingCheckoutSessionKey = "PendingCheckoutState";

        private readonly IMomoService _momoService;
        private readonly IVnPayService _vnPayService;
        private readonly IOrderService _orderService;
        private readonly IInventoryService _inventoryService;
        private readonly ICheckoutPricingService _checkoutPricingService;
        private readonly IMemoryCache _memoryCache;

        public PaymentController(
            IMomoService momoService,
            IVnPayService vnPayService,
            IOrderService orderService,
            IInventoryService inventoryService,
            ICheckoutPricingService checkoutPricingService,
            IMemoryCache memoryCache)
        {
            _momoService = momoService;
            _vnPayService = vnPayService;
            _orderService = orderService;
            _inventoryService = inventoryService;
            _checkoutPricingService = checkoutPricingService;
            _memoryCache = memoryCache;
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePaymentUrlVnpay(CheckoutInputViewModel checkoutModel)
        {
            if (!ModelState.IsValid)
            {
                TempData["error"] = "Vui lòng nhập đầy đủ thông tin đặt hàng.";
                return RedirectToAction("Index", "Cart");
            }

            var pricingSummary = await _checkoutPricingService.BuildSummaryAsync(HttpContext, checkoutModel);
            if (!pricingSummary.CartItems.Any())
            {
                TempData["error"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index", "Cart");
            }

            if (pricingSummary.TotalAmount <= 0)
            {
                TempData["error"] = "Tổng tiền thanh toán VNPAY không hợp lệ.";
                return RedirectToAction("Index", "Cart");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userEmail))
            {
                TempData["error"] = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
            }

            string? reservationCode = null;

            try
            {
                checkoutModel.PaymentMethod = "VNPAY";
                reservationCode = await _inventoryService.ReserveCartAsync(
                    HttpContext,
                    User,
                    "VNPAY",
                    pricingSummary.CartItems);

                var pendingState = BuildPendingCheckoutState(
                    reservationCode,
                    userId,
                    userEmail,
                    checkoutModel,
                    pricingSummary);

                StorePendingCheckoutState(pendingState);
                HttpContext.Session.SetString("ActiveReservationCode", reservationCode);

                var paymentModel = new PaymentInformationModel
                {
                    OrderType = "other",
                    Amount = pricingSummary.TotalAmount,
                    OrderDescription = $"Thanh toán đơn hàng {reservationCode}",
                    Name = checkoutModel.FullName,
                    TxnRef = reservationCode
                };

                var url = _vnPayService.CreatePaymentUrl(paymentModel, HttpContext);
                return Redirect(url);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(reservationCode))
                {
                    await _inventoryService.ReleaseReservationAsync(reservationCode, userId, "Tạo URL VNPAY thất bại.");
                    ClearPendingCheckoutState(reservationCode);
                }

                TempData["error"] = ex.Message;
                return RedirectToAction("Index", "Cart");
            }
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallbackVnpay()
        {
            var response = _vnPayService.PaymentExecute(Request.Query);
            var reservationCode = response?.OrderId;
            var pendingState = await GetPendingCheckoutStateAsync(reservationCode);

            if (string.IsNullOrWhiteSpace(reservationCode))
            {
                reservationCode = pendingState?.ReservationCode
                    ?? HttpContext.Session.GetString("ActiveReservationCode");
            }

            if (pendingState == null && !string.IsNullOrWhiteSpace(reservationCode))
            {
                pendingState = await GetPendingCheckoutStateAsync(reservationCode);
            }

            if (response == null)
            {
                if (!string.IsNullOrWhiteSpace(reservationCode))
                {
                    await _inventoryService.ReleaseReservationAsync(
                        reservationCode,
                        User.FindFirstValue(ClaimTypes.NameIdentifier),
                        "VNPAY không trả về phản hồi.");
                    ClearPendingCheckoutState(reservationCode);
                }

                TempData["error"] = "Không nhận được phản hồi từ VNPAY.";
                return RedirectToAction("Index", "Cart");
            }

            if (!response.SignatureValid)
            {
                if (!string.IsNullOrWhiteSpace(reservationCode))
                {
                    await _inventoryService.ReleaseReservationAsync(
                        reservationCode,
                        pendingState?.UserId ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                        "VNPAY trả về chữ ký không hợp lệ.");
                    ClearPendingCheckoutState(reservationCode);
                }

                TempData["error"] = "Phản hồi từ VNPAY có chữ ký không hợp lệ.";
                return RedirectToAction("Index", "Cart");
            }

            if (!response.Success)
            {
                if (!string.IsNullOrWhiteSpace(reservationCode))
                {
                    await _inventoryService.ReleaseReservationAsync(
                        reservationCode,
                        pendingState?.UserId ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                        $"VNPAY fail: ResponseCode={response.VnPayResponseCode}, TransactionStatus={response.TransactionStatus}");
                    ClearPendingCheckoutState(reservationCode);
                }

                TempData["error"] = $"Thanh toán VNPAY thất bại. Mã lỗi: {response.VnPayResponseCode}";
                return RedirectToAction("Index", "Cart");
            }

            if (pendingState == null || string.IsNullOrWhiteSpace(reservationCode))
            {
                TempData["error"] = $"Thanh toán đã được xác nhận nhưng không tìm thấy dữ liệu checkout tương ứng{(string.IsNullOrWhiteSpace(reservationCode) ? string.Empty : $" ({reservationCode})")}.";
                return RedirectToAction("Index", "Cart");
            }

            if (response.Amount != pendingState.ExpectedTotal)
            {
                await _inventoryService.ReleaseReservationAsync(
                    reservationCode,
                    pendingState.UserId,
                    $"Sai lệch số tiền VNPAY. Expected={pendingState.ExpectedTotal}, Actual={response.Amount}");
                ClearPendingCheckoutState(reservationCode);

                TempData["error"] = "Số tiền thanh toán không khớp với đơn hàng.";
                return RedirectToAction("Index", "Cart");
            }

            try
            {
                var orderCode = await _orderService.CreateOrderFromReservationAsync(
                    HttpContext,
                    User,
                    pendingState.CheckoutInfo,
                    reservationCode,
                    pendingState);

                if (string.IsNullOrEmpty(orderCode))
                {
                    await _inventoryService.ReleaseReservationAsync(
                        reservationCode,
                        pendingState.UserId,
                        "Tạo đơn sau VNPAY thất bại.");
                    ClearPendingCheckoutState(reservationCode);

                    TempData["error"] = "Thanh toán thành công nhưng không thể tạo đơn hàng.";
                    return RedirectToAction("Index", "Cart");
                }

                ClearPendingCheckoutState(reservationCode);
                TempData["success"] = $"Thanh toán VNPAY thành công. Mã đơn: {orderCode}";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                await _inventoryService.ReleaseReservationAsync(
                    reservationCode,
                    pendingState.UserId,
                    "Rollback sau lỗi tạo đơn VNPAY.");
                ClearPendingCheckoutState(reservationCode);

                TempData["error"] = ex.Message;
                return RedirectToAction("Index", "Cart");
            }
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePaymentMomo(OrderInfoModel model, CheckoutInputViewModel checkoutModel)
        {
            if (model == null)
            {
                TempData["error"] = "Dữ liệu thanh toán MoMo không hợp lệ.";
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

            var pricingSummary = await _checkoutPricingService.BuildSummaryAsync(HttpContext, checkoutModel);
            if (!pricingSummary.CartItems.Any())
            {
                TempData["error"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index", "Cart");
            }

            if (pricingSummary.TotalAmount <= 0)
            {
                TempData["error"] = "Tổng tiền thanh toán MoMo không hợp lệ.";
                return RedirectToAction("Index", "Cart");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userEmail))
            {
                TempData["error"] = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Cart") });
            }

            string? reservationCode = null;

            try
            {
                checkoutModel.PaymentMethod = "MOMO";
                model.FullName = checkoutModel.FullName;
                model.Amount = (double)pricingSummary.TotalAmount;
                model.OrderInfomation = string.IsNullOrWhiteSpace(model.OrderInfomation)
                    ? $"Thanh toán đơn hàng qua MOMO cho {checkoutModel.FullName}"
                    : model.OrderInfomation;

                reservationCode = await _inventoryService.ReserveCartAsync(
                    HttpContext,
                    User,
                    "MOMO",
                    pricingSummary.CartItems);

                model.OrderId = reservationCode;

                var pendingState = BuildPendingCheckoutState(
                    reservationCode,
                    userId,
                    userEmail,
                    checkoutModel,
                    pricingSummary);

                StorePendingCheckoutState(pendingState);
                HttpContext.Session.SetString("ActiveReservationCode", reservationCode);

                var response = await _momoService.CreatePaymentAsync(model);

                if (response == null || string.IsNullOrWhiteSpace(response.PayUrl))
                {
                    await _inventoryService.ReleaseReservationAsync(
                        reservationCode,
                        userId,
                        "MoMo không trả về payUrl.");
                    ClearPendingCheckoutState(reservationCode);
                    TempData["error"] = $"MoMo không trả về payUrl. Message: {response?.Message}, ResultCode: {response?.ResultCode}";
                    return RedirectToAction("Index", "Cart");
                }

                return Redirect(response.PayUrl);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(reservationCode))
                {
                    await _inventoryService.ReleaseReservationAsync(
                        reservationCode,
                        userId,
                        "Tạo URL MOMO thất bại.");
                    ClearPendingCheckoutState(reservationCode);
                }

                TempData["error"] = ex.Message;
                return RedirectToAction("Index", "Cart");
            }
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallback()
        {
            var response = _momoService.PaymentExecuteAsync(HttpContext.Request.Query);
            var reservationCode = response?.OrderId;
            var pendingState = await GetPendingCheckoutStateAsync(reservationCode);

            if (string.IsNullOrWhiteSpace(reservationCode))
            {
                reservationCode = pendingState?.ReservationCode
                    ?? HttpContext.Session.GetString("ActiveReservationCode");
            }

            if (pendingState == null && !string.IsNullOrWhiteSpace(reservationCode))
            {
                pendingState = await GetPendingCheckoutStateAsync(reservationCode);
            }

            if (response == null)
            {
                if (!string.IsNullOrWhiteSpace(reservationCode))
                {
                    await _inventoryService.ReleaseReservationAsync(
                        reservationCode,
                        pendingState?.UserId ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                        "MOMO không trả về phản hồi.");
                    ClearPendingCheckoutState(reservationCode);
                }

                TempData["error"] = "Không nhận được phản hồi từ MoMo.";
                return RedirectToAction("Index", "Cart");
            }

            if (!response.Success)
            {
                if (!string.IsNullOrWhiteSpace(reservationCode))
                {
                    await _inventoryService.ReleaseReservationAsync(
                        reservationCode,
                        pendingState?.UserId ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                        $"MOMO fail: {response.ResultCode} {response.Message}");
                    ClearPendingCheckoutState(reservationCode);
                }

                TempData["error"] = $"Thanh toán MoMo thất bại. Mã lỗi: {response.ResultCode}";
                return RedirectToAction("Index", "Cart");
            }

            if (pendingState == null || string.IsNullOrWhiteSpace(reservationCode))
            {
                TempData["error"] = "Không tìm thấy thông tin checkout hoặc reservation.";
                return RedirectToAction("Index", "Cart");
            }

            if (!TryParsePaymentAmount(response.Amount, out var paidAmount) || paidAmount != pendingState.ExpectedTotal)
            {
                await _inventoryService.ReleaseReservationAsync(
                    reservationCode,
                    pendingState.UserId,
                    $"Sai lệch số tiền MOMO. Expected={pendingState.ExpectedTotal}, Actual={response.Amount}");
                ClearPendingCheckoutState(reservationCode);

                TempData["error"] = "Số tiền thanh toán không khớp với đơn hàng.";
                return RedirectToAction("Index", "Cart");
            }

            try
            {
                var orderCode = await _orderService.CreateOrderFromReservationAsync(
                    HttpContext,
                    User,
                    pendingState.CheckoutInfo,
                    reservationCode,
                    pendingState);

                if (string.IsNullOrEmpty(orderCode))
                {
                    await _inventoryService.ReleaseReservationAsync(
                        reservationCode,
                        pendingState.UserId,
                        "Tạo đơn sau MOMO thất bại.");
                    ClearPendingCheckoutState(reservationCode);

                    TempData["error"] = "Thanh toán thành công nhưng không thể tạo đơn hàng.";
                    return RedirectToAction("Index", "Cart");
                }

                ClearPendingCheckoutState(reservationCode);
                TempData["success"] = $"Thanh toán MOMO thành công. Mã đơn: {orderCode}";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                await _inventoryService.ReleaseReservationAsync(
                    reservationCode,
                    pendingState.UserId,
                    "Rollback sau lỗi tạo đơn MOMO.");
                ClearPendingCheckoutState(reservationCode);

                TempData["error"] = ex.Message;
                return RedirectToAction("Index", "Cart");
            }
        }

        [HttpPost]
        public IActionResult MomoNotify()
        {
            return Ok();
        }

        private string BuildPendingCheckoutCacheKey(string reservationCode)
        {
            return $"{PendingCheckoutCacheKeyPrefix}{reservationCode}";
        }

        private PendingCheckoutStateViewModel BuildPendingCheckoutState(
            string reservationCode,
            string userId,
            string userEmail,
            CheckoutInputViewModel checkoutModel,
            CheckoutPricingSummaryViewModel pricingSummary)
        {
            return new PendingCheckoutStateViewModel
            {
                ReservationCode = reservationCode,
                UserId = userId,
                UserEmail = userEmail,
                UserName = User.Identity?.Name,
                CheckoutInfo = checkoutModel,
                CartItems = pricingSummary.CartItems,
                ExpectedTotal = pricingSummary.TotalAmount,
                SubTotal = pricingSummary.SubTotal,
                ShippingCost = pricingSummary.ShippingCost,
                DiscountAmount = pricingSummary.DiscountAmount,
                CouponCode = pricingSummary.CouponCode,
                CouponId = pricingSummary.CouponId
            };
        }

        private void StorePendingCheckoutState(PendingCheckoutStateViewModel pendingState)
        {
            _memoryCache.Set(
                BuildPendingCheckoutCacheKey(pendingState.ReservationCode),
                pendingState,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });

            HttpContext.Session.SetJson(PendingCheckoutSessionKey, pendingState);
            HttpContext.Session.SetJson("CheckoutInfo", pendingState.CheckoutInfo);
        }

        private void ClearPendingCheckoutState(string? reservationCode)
        {
            if (!string.IsNullOrWhiteSpace(reservationCode))
            {
                _memoryCache.Remove(BuildPendingCheckoutCacheKey(reservationCode));
            }

            HttpContext.Session.Remove(PendingCheckoutSessionKey);
            HttpContext.Session.Remove("CheckoutInfo");
            HttpContext.Session.Remove("ActiveReservationCode");
        }

        private Task<PendingCheckoutStateViewModel?> GetPendingCheckoutStateAsync(string? reservationCode)
        {
            if (!string.IsNullOrWhiteSpace(reservationCode) &&
                _memoryCache.TryGetValue(BuildPendingCheckoutCacheKey(reservationCode), out PendingCheckoutStateViewModel? cachedState) &&
                cachedState != null)
            {
                return Task.FromResult<PendingCheckoutStateViewModel?>(cachedState);
            }

            var sessionState = HttpContext.Session.GetJson<PendingCheckoutStateViewModel>(PendingCheckoutSessionKey);
            if (sessionState == null)
            {
                return Task.FromResult<PendingCheckoutStateViewModel?>(null);
            }

            if (string.IsNullOrWhiteSpace(reservationCode) ||
                string.Equals(sessionState.ReservationCode, reservationCode, StringComparison.Ordinal))
            {
                return Task.FromResult<PendingCheckoutStateViewModel?>(sessionState);
            }

            return Task.FromResult<PendingCheckoutStateViewModel?>(null);
        }

        private static bool TryParsePaymentAmount(string? rawAmount, out decimal amount)
        {
            if (decimal.TryParse(rawAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                return true;
            }

            return decimal.TryParse(rawAmount, out amount);
        }
    }
}