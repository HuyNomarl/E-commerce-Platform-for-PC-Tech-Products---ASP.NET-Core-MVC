using Eshop.Models;

namespace Eshop.Services
{
    public class OrderStateService : IOrderStateService
    {
        private static readonly IReadOnlyDictionary<OrderStatus, IReadOnlyList<OrderStatus>> TransitionMap =
            new Dictionary<OrderStatus, IReadOnlyList<OrderStatus>>
            {
                [OrderStatus.Pending] = new[] { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Cancelled },
                [OrderStatus.Processing] = new[] { OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Cancelled },
                [OrderStatus.Shipped] = new[] { OrderStatus.Shipped, OrderStatus.Delivered },
                [OrderStatus.Delivered] = new[] { OrderStatus.Delivered, OrderStatus.Completed },
                [OrderStatus.Completed] = new[] { OrderStatus.Completed },
                [OrderStatus.Cancelled] = new[] { OrderStatus.Cancelled }
            };

        public (bool IsValid, string Message) ValidateTransition(OrderStatus oldStatus, OrderStatus newStatus)
        {
            if (oldStatus == newStatus)
                return (true, "Không có thay đổi.");

            if (oldStatus == OrderStatus.Cancelled)
                return (false, "Đơn đã hủy, không thể mở lại.");

            if (oldStatus == OrderStatus.Completed)
                return (false, "Đơn đã hoàn tất, không thể đổi trạng thái.");

            return (oldStatus, newStatus) switch
            {
                (OrderStatus.Pending, OrderStatus.Processing) => (true, string.Empty),
                (OrderStatus.Pending, OrderStatus.Cancelled) => (true, string.Empty),

                (OrderStatus.Processing, OrderStatus.Shipped) => (true, string.Empty),
                (OrderStatus.Processing, OrderStatus.Cancelled) => (true, string.Empty),

                (OrderStatus.Shipped, OrderStatus.Delivered) => (true, string.Empty),

                (OrderStatus.Delivered, OrderStatus.Completed) => (true, string.Empty),

                _ => (false, $"Không được chuyển trạng thái từ {oldStatus} sang {newStatus}.")
            };
        }

        public IReadOnlyList<OrderStatus> GetAvailableStatuses(OrderStatus currentStatus)
        {
            if (TransitionMap.TryGetValue(currentStatus, out var nextStatuses))
            {
                return nextStatuses;
            }

            return new[] { currentStatus };
        }

        public bool CanCustomerCancel(OrderStatus status)
        {
            return status == OrderStatus.Pending;
        }
    }
}
