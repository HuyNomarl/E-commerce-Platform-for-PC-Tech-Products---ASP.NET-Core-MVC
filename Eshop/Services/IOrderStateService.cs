using Eshop.Models;

namespace Eshop.Services
{
    public interface IOrderStateService
    {
        (bool IsValid, string Message) ValidateTransition(OrderStatus oldStatus, OrderStatus newStatus);
        bool CanCustomerCancel(OrderStatus status);
    }
}
