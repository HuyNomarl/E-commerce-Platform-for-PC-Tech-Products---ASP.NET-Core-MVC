using Eshop.Models;

namespace Eshop.Helpers
{
    public static class OrderDisplayHelper
    {
        public static OrderStatus ToOrderStatus(int statusValue)
        {
            return Enum.IsDefined(typeof(OrderStatus), statusValue)
                ? (OrderStatus)statusValue
                : OrderStatus.Pending;
        }

        public static string GetStatusLabel(int statusValue)
        {
            return GetStatusLabel(ToOrderStatus(statusValue));
        }

        public static string GetStatusLabel(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Pending => "Chờ xác nhận",
                OrderStatus.Processing => "Đang xử lý",
                OrderStatus.Shipped => "Đang giao",
                OrderStatus.Delivered => "Đã giao",
                OrderStatus.Completed => "Hoàn thành",
                OrderStatus.Cancelled => "Đã hủy",
                _ => "Không xác định"
            };
        }

        public static string GetStatusDescription(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Pending => "Đơn hàng mới tạo và đang chờ đội ngũ xác nhận.",
                OrderStatus.Processing => "Đơn hàng đã được xác nhận và đang chuẩn bị xuất kho.",
                OrderStatus.Shipped => "Đơn hàng đã bàn giao cho đơn vị vận chuyển.",
                OrderStatus.Delivered => "Đơn hàng đã giao thành công tới khách hàng.",
                OrderStatus.Completed => "Đơn hàng đã hoàn tất toàn bộ quy trình.",
                OrderStatus.Cancelled => "Đơn hàng đã bị hủy và không còn hiệu lực.",
                _ => "Không xác định được trạng thái đơn hàng."
            };
        }

        public static string GetBootstrapBadgeClass(int statusValue)
        {
            return GetBootstrapBadgeClass(ToOrderStatus(statusValue));
        }

        public static string GetBootstrapBadgeClass(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Pending => "bg-warning text-dark",
                OrderStatus.Processing => "bg-info text-dark",
                OrderStatus.Shipped => "bg-primary",
                OrderStatus.Delivered => "bg-success",
                OrderStatus.Completed => "bg-success",
                OrderStatus.Cancelled => "bg-danger",
                _ => "bg-secondary"
            };
        }

        public static string GetSoftBadgeClass(int statusValue)
        {
            return GetSoftBadgeClass(ToOrderStatus(statusValue));
        }

        public static string GetSoftBadgeClass(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Pending => "badge-warn",
                OrderStatus.Processing => "badge-info",
                OrderStatus.Shipped => "badge-show",
                OrderStatus.Delivered => "badge-success",
                OrderStatus.Completed => "badge-success-dark",
                OrderStatus.Cancelled => "badge-delete",
                _ => "badge-secondary"
            };
        }

        public static string NormalizePaymentMethod(string? paymentMethod)
        {
            return string.IsNullOrWhiteSpace(paymentMethod)
                ? string.Empty
                : paymentMethod.Trim().ToUpperInvariant();
        }

        public static string GetPaymentMethodLabel(string? paymentMethod)
        {
            return NormalizePaymentMethod(paymentMethod) switch
            {
                "COD" => "Thanh toán khi nhận hàng",
                "VNPAY" => "VNPAY",
                "MOMO" => "MoMo",
                "BANK_TRANSFER" => "Chuyển khoản ngân hàng",
                "CARD" => "Thẻ ngân hàng",
                _ => string.IsNullOrWhiteSpace(paymentMethod)
                    ? "Chưa xác định"
                    : paymentMethod.Trim()
            };
        }

        public static string GetPaymentMethodBadgeClass(string? paymentMethod)
        {
            return NormalizePaymentMethod(paymentMethod) switch
            {
                "COD" => "payment-badge payment-badge-cod",
                "VNPAY" => "payment-badge payment-badge-vnpay",
                "MOMO" => "payment-badge payment-badge-momo",
                "BANK_TRANSFER" => "payment-badge payment-badge-bank",
                "CARD" => "payment-badge payment-badge-bank",
                _ => "payment-badge payment-badge-default"
            };
        }
    }
}
