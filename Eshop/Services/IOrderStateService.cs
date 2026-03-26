using Eshop.Models;

namespace Eshop.Services
{
    public interface IOrderStateService
    {
        (bool IsValid, string Message) ValidateTransition(OrderStatus oldStatus, OrderStatus newStatus);
        IReadOnlyList<OrderStatus> GetAvailableStatuses(OrderStatus currentStatus);
        bool CanCustomerCancel(OrderStatus status);
    }
}
