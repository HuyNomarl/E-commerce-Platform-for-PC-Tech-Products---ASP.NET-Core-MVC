using Eshop.Areas.Admin.Repository;
using Eshop.Helpers;
using Eshop.Hubs;
using Eshop.Models;
using Eshop.Models.ViewModel;
using Eshop.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            DataContext dataContext,
            IEmailSender emailSender,
            IHubContext<NotificationHub> notificationHub,
            IInventoryService inventoryService,
            ILogger<OrderService> logger)
        {
            _dataContext = dataContext;
            _emailSender = emailSender;
            _notificationHub = notificationHub;
            _inventoryService = inventoryService;
            _logger = logger;
        }

        public async Task<string?> CreateOrderFromSessionAsync(HttpContext httpContext, ClaimsPrincipal user, CheckoutInputViewModel model)
        {
            var paymentMethod = (model.PaymentMethod ?? "COD").Trim().ToUpperInvariant();

            if (paymentMethod == "VNPAY" || paymentMethod == "MOMO")
                throw new InvalidOperationException("Thanh toán online phải đi qua luồng reservation trước khi tạo đơn.");

            var reservationCode = await _inventoryService.ReserveCartAsync(httpContext, user, paymentMethod);

            try
            {
                return await CreateOrderFromReservationAsync(httpContext, user, model, reservationCode);
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

        public async Task<string?> CreateOrderFromReservationAsync(HttpContext httpContext, ClaimsPrincipal user, CheckoutInputViewModel model, string reservationCode)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = user.FindFirstValue(ClaimTypes.Email);
            var userName = user.Identity?.Name;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail))
                return null;

            var cartItems = httpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            if (cartItems.Count == 0)
                return null;

            await using var transaction = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                var productIds = cartItems.Select(x => (int)x.ProductId).Distinct().ToList();

                var products = await _dataContext.Products
                    .Where(x => productIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                var subTotal = cartItems.Sum(x => x.Quantity * x.Price);
                var shippingCost = ReadShippingCost(httpContext);

                decimal discountAmount = 0m;
                string? couponCode = null;

                var appliedCoupon = httpContext.Session.GetJson<AppliedCouponModel>("Coupon");
                if (appliedCoupon != null)
                {
                    var coupon = await _dataContext.Coupons.FirstOrDefaultAsync(x => x.Id == appliedCoupon.CouponId);

                    if (coupon != null)
                    {
                        var now = DateTime.Now;
                        var isValid =
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
                            httpContext.Session.Remove("Coupon");
                        }
                    }
                    else
                    {
                        httpContext.Session.Remove("Coupon");
                    }
                }

                var totalAmount = subTotal - discountAmount + shippingCost;
                if (totalAmount < 0)
                {
                    totalAmount = 0;
                }

                var orderCode = Guid.NewGuid().ToString("N");
                var createdAt = DateTime.Now;
                var fullAddress = $"{model.Address}, {model.phuong}"
                    + $"{(string.IsNullOrWhiteSpace(model.quan) ? string.Empty : ", " + model.quan)}"
                    + $", {model.tinh}";

                var order = new OrderModel
                {
                    UserId = userId,
                    UserName = userName ?? userEmail,
                    OrderCode = orderCode,
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Email = model.Email,
                    Address = fullAddress,
                    Province = model.tinh,
                    District = model.quan,
                    Ward = model.phuong,
                    Note = model.Note,
                    Status = (int)OrderStatus.Pending,
                    CreatedTime = createdAt,
                    SubTotal = subTotal,
                    DiscountAmount = discountAmount,
                    CouponCode = couponCode,
                    ShippingCost = shippingCost,
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
                        ProductImage = product.Image,
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

                httpContext.Session.Remove("Cart");
                httpContext.Session.Remove("Coupon");
                httpContext.Session.Remove("CheckoutInfo");
                httpContext.Session.Remove("ActiveReservationCode");
                httpContext.Response.Cookies.Delete("ShippingPrice");

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

        private static decimal ReadShippingCost(HttpContext httpContext)
        {
            var shippingCookie = httpContext.Request.Cookies["ShippingPrice"];

            if (string.IsNullOrWhiteSpace(shippingCookie))
            {
                return 0m;
            }

            try
            {
                var shippingCost = Newtonsoft.Json.JsonConvert.DeserializeObject<decimal>(shippingCookie);
                return shippingCost < 0 ? 0m : shippingCost;
            }
            catch
            {
                return 0m;
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
            var sb = new StringBuilder();
            var orderUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/Account/Details?orderCode={Uri.EscapeDataString(order.OrderCode)}";

            sb.Append("""
<div style="font-family:Arial,Helvetica,sans-serif;background:#f5f7fb;padding:24px;color:#111827;">
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
""");

            sb.Append("""
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
