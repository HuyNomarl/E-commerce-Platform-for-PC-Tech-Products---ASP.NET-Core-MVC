using Eshop.Models.ViewModel;
using System.Security.Claims;

namespace Eshop.Services
{
    public interface IOrderService
    {
        Task<string?> CreateOrderFromSessionAsync(HttpContext httpContext, ClaimsPrincipal user, CheckoutInputViewModel model);
    }
}