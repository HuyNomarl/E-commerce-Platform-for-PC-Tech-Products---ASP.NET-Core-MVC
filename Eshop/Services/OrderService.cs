using Eshop.Areas.Admin.Repository;
using Eshop.Helpers;
using Eshop.Hubs;
using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Eshop.Services
{
    public class OrderService : IOrderService
    {
        private readonly DataContext _dataContext;
        private readonly IEmailSender _emailSender;
        private readonly IHubContext<NotificationHub> _notificationHub;
        private readonly IInventoryService _inventoryService;
        private readonly ICheckoutPricingService _checkoutPricingService;
        private readonly ICartService _cartService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            DataContext dataContext,
            IEmailSender emailSender,
            IHubContext<NotificationHub> notificationHub,
            IInventoryService inventoryService,
            ICheckoutPricingService checkoutPricingService,
            ICartService cartService,
            ILogger<OrderService> logger)
        {
            _dataContext = dataContext;
            _emailSender = emailSender;
            _notificationHub = notificationHub;
            _inventoryService = inventoryService;
            _checkoutPricingService = checkoutPricingService;
            _cartService = cartService;
            _logger = logger;
        }

        public async Task<string?> CreateOrderFromCartAsync(HttpContext httpContext, ClaimsPrincipal user, CheckoutInputViewModel model)
        {
            var paymentMethod = (model.PaymentMethod ?? "COD").Trim().ToUpperInvariant();

            if (paymentMethod == "VNPAY" || paymentMethod == "MOMO")
                throw new InvalidOperationException("Thanh toán online phải đi qua luồng giữ chỗ trước khi tạo đơn.");

            var pricingSummary = await _checkoutPricingService.BuildSummaryAsync(httpContext, model);
            if (!pricingSummary.CartItems.Any())
            {
                return null;
            }

            var pendingState = new PendingCheckoutStateViewModel
            {
                UserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                UserEmail = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                UserName = user.Identity?.Name,
                CheckoutInfo = model,
                CartItems = pricingSummary.CartItems,
                ExpectedTotal = pricingSummary.TotalAmount,
                SubTotal = pricingSummary.SubTotal,
                ShippingCost = pricingSummary.ShippingCost,
                DiscountAmount = pricingSummary.DiscountAmount,
                CouponCode = pricingSummary.CouponCode,
                CouponId = pricingSummary.CouponId
            };

            var reservationCode = await _inventoryService.ReserveCartAsync(
                httpContext,
                user,
                paymentMethod,
                pricingSummary.CartItems);

            pendingState.ReservationCode = reservationCode;

            try
            {
                return await CreateOrderFromReservationAsync(httpContext, user, model, reservationCode, pendingState);
            }
            catch
            {
                await _inventoryService.ReleaseReservationAsync(
                    reservationCode,
                    user.FindFirstValue(ClaimTypes.NameIdentifier),
                    "Tạo đơn COD thất bại.");
                throw;
            }
        }

        public async Task<string?> CreateOrderFromReservationAsync(
            HttpContext httpContext,
            ClaimsPrincipal user,
            CheckoutInputViewModel model,
            string reservationCode,
            PendingCheckoutStateViewModel? pendingState = null)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? pendingState?.UserId;
            var userEmail = user.FindFirstValue(ClaimTypes.Email) ?? pendingState?.UserEmail;
            var userName = user.Identity?.Name ?? pendingState?.UserName;

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userEmail))
                return null;

            var cartItems = pendingState?.CartItems
                ?? await _cartService.GetCartAsync(httpContext, userId);

            if (cartItems.Count == 0)
                return null;

            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                var productIds = cartItems.Select(x => (int)x.ProductId).Distinct().ToList();
                var products = await _dataContext.Products
                    .Include(x => x.ProductImages)
                    .Where(x => productIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                var pricingSummary = pendingState == null
                    ? await _checkoutPricingService.BuildSummaryAsync(httpContext, model)
                    : new CheckoutPricingSummaryViewModel
                    {
                        CartItems = pendingState.CartItems,
                        SubTotal = pendingState.SubTotal,
                        ShippingCost = pendingState.ShippingCost,
                        DiscountAmount = pendingState.DiscountAmount,
                        TotalAmount = pendingState.ExpectedTotal,
                        CouponCode = pendingState.CouponCode,
                        CouponId = pendingState.CouponId,
                        ShippingSelection = new CheckoutShippingSelectionViewModel
                        {
                            ProvinceCode = model.ProvinceCode,
                            WardCode = model.WardCode,
                            ProvinceName = model.tinh,
                            DistrictName = model.quan,
                            WardName = model.phuong
                        }
                    };

                if (pricingSummary.CouponId.HasValue)
                {
                    var coupon = await _dataContext.Coupons.FirstOrDefaultAsync(x => x.Id == pricingSummary.CouponId.Value);
                    if (coupon != null && coupon.Quantity > 0)
                    {
                        coupon.Quantity -= 1;
                    }
                }

                var provinceName = pricingSummary.ShippingSelection?.ProvinceName ?? model.tinh;
                var districtName = !string.IsNullOrWhiteSpace(model.quan)
                    ? model.quan
                    : pricingSummary.ShippingSelection?.DistrictName;
                var wardName = pricingSummary.ShippingSelection?.WardName ?? model.phuong;
                var totalAmount = pricingSummary.TotalAmount < 0 ? 0 : pricingSummary.TotalAmount;
                var orderCode = Guid.NewGuid().ToString("N");
                var createdAt = DateTime.Now;
                var fullAddress = $"{model.Address}, {wardName}"
                    + $"{(string.IsNullOrWhiteSpace(districtName) ? string.Empty : ", " + districtName)}"
                    + $", {provinceName}";

                var order = new OrderModel
                {
                    UserId = userId,
                    UserName = userName ?? userEmail,
                    OrderCode = orderCode,
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Email = model.Email,
                    Address = fullAddress,
                    Province = provinceName,
                    District = districtName,
                    Ward = wardName,
                    Note = model.Note,
                    Status = (int)OrderStatus.Pending,
                    CreatedTime = createdAt,
                    SubTotal = pricingSummary.SubTotal,
                    DiscountAmount = pricingSummary.DiscountAmount,
                    CouponCode = pricingSummary.CouponCode,
                    ShippingCost = pricingSummary.ShippingCost,
                    TotalAmount = totalAmount,
                    PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod)
                        ? "COD"
                        : model.PaymentMethod.Trim().ToUpperInvariant(),
                    ReservationCode = reservationCode
                };

                _dataContext.Orders.Add(order);
                await _dataContext.SaveChangesAsync();

                var createdDetails = new List<OrderDetails>();

                foreach (var cart in cartItems)
                {
                    if (!products.TryGetValue((int)cart.ProductId, out var product))
                        throw new InvalidOperationException($"Sản phẩm #{cart.ProductId} không tồn tại.");

                    var detail = new OrderDetails
                    {
                        OrderId = order.OrderId,
                        ProductId = (int)cart.ProductId,
                        ProductName = product.Name,
                        ProductImage = ProductImageHelper.ResolveProductImage(product),
                        Price = cart.Price,
                        Quantity = cart.Quantity,
                        BuildGroupKey = cart.BuildGroupKey,
                        PcBuildId = cart.PcBuildId,
                        BuildName = cart.BuildName,
                        ComponentType = cart.ComponentType
                    };

                    createdDetails.Add(detail);
                    _dataContext.OrderDetails.Add(detail);
                }

                await _dataContext.SaveChangesAsync();
                await _inventoryService.CommitReservationAsync(reservationCode, orderCode, userId);
                await _dataContext.SaveChangesAsync();
                await transaction.CommitAsync();

                await _cartService.RemovePurchasedItemsAsync(httpContext, cartItems, userId);
                httpContext.Session.Remove("Coupon");
                httpContext.Session.Remove("CheckoutInfo");
                httpContext.Session.Remove("PendingCheckoutState");
                httpContext.Session.Remove("ActiveReservationCode");
                _checkoutPricingService.ClearShippingSelection(httpContext);

                await TryPushAdminNotificationAsync(orderCode, model.FullName, model.Phone, totalAmount, createdAt);
                await TrySendOrderConfirmationEmailAsync(httpContext, order, createdDetails);

                return orderCode;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task TryPushAdminNotificationAsync(string orderCode, string? fullName, string? phone, decimal totalAmount, DateTime createdAt)
        {
            try
            {
                await _notificationHub.Clients
                    .Group(Eshop.Constants.NotificationGroups.OrderManagers)
                    .SendAsync("NewOrderCreated", new
                    {
                        orderCode,
                        fullName,
                        phone,
                        totalAmount = totalAmount.ToString("N0"),
                        createdAt = createdAt.ToString("dd/MM/yyyy HH:mm"),
                        url = $"/Admin/Order/ViewOrder?orderCode={orderCode}"
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi realtime notification cho đơn hàng {OrderCode}.", orderCode);
            }
        }

        private async Task TrySendOrderConfirmationEmailAsync(HttpContext httpContext, OrderModel order, IReadOnlyCollection<OrderDetails> details)
        {
            if (string.IsNullOrWhiteSpace(order.Email))
            {
                return;
            }

            try
            {
                await _emailSender.SendEmailAsync(
                    order.Email,
                    $"[Eshop] Xác nhận đơn hàng {order.OrderCode}",
                    BuildOrderConfirmationEmail(httpContext, order, details));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email xác nhận cho đơn hàng {OrderCode}.", order.OrderCode);
            }
        }

        private static string BuildOrderConfirmationEmail(HttpContext httpContext, OrderModel order, IReadOnlyCollection<OrderDetails> details)
        {
            var orderUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/Account/Details?orderCode={Uri.EscapeDataString(order.OrderCode)}";
            var sb = new StringBuilder();

            sb.Append("""
<div style="font-family:'Inter','Segoe UI','Helvetica Neue',sans-serif;background:#f5f7fb;padding:24px;color:#111827;">
    <div style="max-width:760px;margin:0 auto;background:#ffffff;border-radius:18px;overflow:hidden;border:1px solid #e5e7eb;">
        <div style="padding:24px 28px;background:linear-gradient(135deg,#0f172a,#1d4ed8);color:#ffffff;">
            <div style="font-size:13px;letter-spacing:.08em;text-transform:uppercase;opacity:.85;">Eshop</div>
            <h1 style="margin:10px 0 6px;font-size:24px;">Xác nhận đặt hàng thành công</h1>
            <div style="font-size:15px;opacity:.9;">Cảm ơn bạn đã mua sắm. Đơn hàng của bạn đã được ghi nhận.</div>
        </div>
        <div style="padding:24px 28px;">
""");

            sb.Append($"""
            <p style="margin:0 0 16px;font-size:15px;line-height:1.7;">
                Xin chào <strong>{HtmlEncode(string.IsNullOrWhiteSpace(order.FullName) ? order.UserName : order.FullName)}</strong>,
                chúng tôi đã nhận được đơn hàng <strong>{HtmlEncode(order.OrderCode)}</strong>.
            </p>

            <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:12px;margin-bottom:20px;">
                <div style="padding:14px;border:1px solid #e5e7eb;border-radius:14px;background:#f8fafc;">
                    <div style="font-size:12px;color:#6b7280;margin-bottom:6px;">Ngày đặt</div>
                    <div style="font-size:16px;font-weight:700;">{order.CreatedTime:dd/MM/yyyy HH:mm}</div>
                </div>
                <div style="padding:14px;border:1px solid #e5e7eb;border-radius:14px;background:#f8fafc;">
                    <div style="font-size:12px;color:#6b7280;margin-bottom:6px;">Trạng thái hiện tại</div>
                    <div style="font-size:16px;font-weight:700;">{HtmlEncode(OrderDisplayHelper.GetStatusLabel(order.Status))}</div>
                </div>
                <div style="padding:14px;border:1px solid #e5e7eb;border-radius:14px;background:#f8fafc;">
                    <div style="font-size:12px;color:#6b7280;margin-bottom:6px;">Phương thức thanh toán</div>
                    <div style="font-size:16px;font-weight:700;">{HtmlEncode(OrderDisplayHelper.GetPaymentMethodLabel(order.PaymentMethod))}</div>
                </div>
            </div>

            <table style="width:100%;border-collapse:collapse;margin-bottom:18px;">
                <thead>
                    <tr style="background:#f8fafc;">
                        <th style="padding:12px;text-align:left;border-bottom:1px solid #e5e7eb;">Sản phẩm</th>
                        <th style="padding:12px;text-align:right;border-bottom:1px solid #e5e7eb;">Đơn giá</th>
                        <th style="padding:12px;text-align:center;border-bottom:1px solid #e5e7eb;">SL</th>
                        <th style="padding:12px;text-align:right;border-bottom:1px solid #e5e7eb;">Thành tiền</th>
                    </tr>
                </thead>
                <tbody>
""");

            foreach (var item in details)
            {
                var productName = item.ProductName;

                if (!string.IsNullOrWhiteSpace(item.BuildName))
                {
                    productName += $" - {item.BuildName}";
                }

                if (!string.IsNullOrWhiteSpace(item.ComponentType))
                {
                    productName += $" ({item.ComponentType})";
                }

                sb.Append($"""
                    <tr>
                        <td style="padding:12px;border-bottom:1px solid #eef2f7;">{HtmlEncode(productName)}</td>
                        <td style="padding:12px;border-bottom:1px solid #eef2f7;text-align:right;">{item.Price:N0} đ</td>
                        <td style="padding:12px;border-bottom:1px solid #eef2f7;text-align:center;">{item.Quantity}</td>
                        <td style="padding:12px;border-bottom:1px solid #eef2f7;text-align:right;font-weight:700;">{(item.Price * item.Quantity):N0} đ</td>
                    </tr>
""");
            }

            sb.Append("""
                </tbody>
            </table>
""");

            sb.Append($"""
            <div style="margin-left:auto;max-width:320px;">
                <div style="display:flex;justify-content:space-between;margin-bottom:8px;font-size:14px;">
                    <span>Tạm tính</span>
                    <strong>{order.SubTotal:N0} đ</strong>
                </div>
                <div style="display:flex;justify-content:space-between;margin-bottom:8px;font-size:14px;">
                    <span>Giảm giá</span>
                    <strong>{order.DiscountAmount:N0} đ</strong>
                </div>
                <div style="display:flex;justify-content:space-between;margin-bottom:8px;font-size:14px;">
                    <span>Phí vận chuyển</span>
                    <strong>{order.ShippingCost:N0} đ</strong>
                </div>
                <div style="display:flex;justify-content:space-between;padding-top:10px;border-top:1px solid #e5e7eb;font-size:17px;">
                    <span>Tổng thanh toán</span>
                    <strong style="color:#dc2626;">{order.TotalAmount:N0} đ</strong>
                </div>
            </div>
""");

            if (!string.IsNullOrWhiteSpace(order.Address))
            {
                sb.Append($"""
            <div style="margin-top:22px;padding:16px;border:1px solid #e5e7eb;border-radius:14px;background:#f8fafc;">
                <div style="font-size:12px;color:#6b7280;margin-bottom:6px;">Địa chỉ giao hàng</div>
                <div style="font-size:15px;font-weight:600;">{HtmlEncode(order.Address)}</div>
            </div>
""");
            }

            if (!string.IsNullOrWhiteSpace(order.Note))
            {
                sb.Append($"""
            <div style="margin-top:16px;padding:16px;border:1px solid #e5e7eb;border-radius:14px;background:#fff7ed;">
                <div style="font-size:12px;color:#9a3412;margin-bottom:6px;">Ghi chú đơn hàng</div>
                <div style="font-size:14px;font-weight:600;color:#7c2d12;">{HtmlEncode(order.Note)}</div>
            </div>
""");
            }

            sb.Append($"""
            <div style="margin-top:24px;">
                <a href="{HtmlEncode(orderUrl)}"
                   style="display:inline-block;padding:12px 18px;border-radius:12px;background:#111827;color:#ffffff;text-decoration:none;font-weight:700;">
                    Xem chi tiết đơn hàng
                </a>
            </div>
        </div>
    </div>
</div>
""");

            return sb.ToString();
        }

        private static string HtmlEncode(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
