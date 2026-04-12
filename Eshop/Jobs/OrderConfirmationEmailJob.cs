using Eshop.Areas.Admin.Repository;
using Eshop.Helpers;
using Eshop.Models;
using Eshop.Repository;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;

namespace Eshop.Jobs
{
    public class OrderConfirmationEmailJob
    {
        private readonly DataContext _dataContext;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<OrderConfirmationEmailJob> _logger;

        public OrderConfirmationEmailJob(
            DataContext dataContext,
            IEmailSender emailSender,
            ILogger<OrderConfirmationEmailJob> logger)
        {
            _dataContext = dataContext;
            _emailSender = emailSender;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task SendAsync(string orderCode, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return;
            }

            var order = await _dataContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderCode == orderCode);

            if (order == null)
            {
                _logger.LogWarning("Không tìm thấy đơn hàng {OrderCode} để gửi mail xác nhận.", orderCode);
                return;
            }

            if (string.IsNullOrWhiteSpace(order.Email))
            {
                return;
            }

            var details = await _dataContext.OrderDetails
                .AsNoTracking()
                .Where(x => x.OrderId == order.OrderId)
                .ToListAsync();

            await _emailSender.SendEmailAsync(
                order.Email,
                $"[Eshop] Xac nhan don hang {order.OrderCode}",
                BuildOrderConfirmationEmail(baseUrl, order, details));
        }

        private static string BuildOrderConfirmationEmail(
            string baseUrl,
            OrderModel order,
            IReadOnlyCollection<OrderDetails> details)
        {
            var normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? string.Empty
                : baseUrl.TrimEnd('/');
            var orderUrl = $"{normalizedBaseUrl}/Account/Details?orderCode={Uri.EscapeDataString(order.OrderCode)}";
            var sb = new StringBuilder();

            sb.Append("""
<div style="font-family:'Inter','Segoe UI','Helvetica Neue',sans-serif;background:#f5f7fb;padding:24px;color:#111827;">
    <div style="max-width:760px;margin:0 auto;background:#ffffff;border-radius:18px;overflow:hidden;border:1px solid #e5e7eb;">
        <div style="padding:24px 28px;background:linear-gradient(135deg,#0f172a,#1d4ed8);color:#ffffff;">
            <div style="font-size:13px;letter-spacing:.08em;text-transform:uppercase;opacity:.85;">Eshop</div>
            <h1 style="margin:10px 0 6px;font-size:24px;">Xác nhận đặt hàng thành công</h1>
            <div style="font-size:15px;opacity:.9;">Cảm ơn bạn đã mua sắm. Đơn hàng của bạn đã được ghi nhận..</div>
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
                        <td style="padding:12px;border-bottom:1px solid #eef2f7;text-align:right;">{item.Price:N0} d</td>
                        <td style="padding:12px;border-bottom:1px solid #eef2f7;text-align:center;">{item.Quantity}</td>
                        <td style="padding:12px;border-bottom:1px solid #eef2f7;text-align:right;font-weight:700;">{(item.Price * item.Quantity):N0} d</td>
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
                    <strong>{order.SubTotal:N0} d</strong>
                </div>
                <div style="display:flex;justify-content:space-between;margin-bottom:8px;font-size:14px;">
                    <span>Giảm giá</span>
                    <strong>{order.DiscountAmount:N0} d</strong>
                </div>
                <div style="display:flex;justify-content:space-between;margin-bottom:8px;font-size:14px;">
                    <span>Phí vận chuyển</span>
                    <strong>{order.ShippingCost:N0} d</strong>
                </div>
                <div style="display:flex;justify-content:space-between;padding-top:10px;border-top:1px solid #e5e7eb;font-size:17px;">
                    <span>Tổng thanh toán</span>
                    <strong style="color:#dc2626;">{order.TotalAmount:N0} d</strong>
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

            if (!string.IsNullOrWhiteSpace(normalizedBaseUrl))
            {
                sb.Append($"""
            <div style="margin-top:24px;">
                <a href="{HtmlEncode(orderUrl)}"
                   style="display:inline-block;padding:12px 18px;border-radius:12px;background:#111827;color:#ffffff;text-decoration:none;font-weight:700;">
                    Xem chi tiết đơn hàng
                </a>
            </div>
""");
            }

            sb.Append("""
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
