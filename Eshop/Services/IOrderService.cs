using System.Security.Claims;

namespace Eshop.Services
{
    public interface IOrderService
    {
        Task<string?> CreateOrderFromSessionAsync(HttpContext httpContext, ClaimsPrincipal user);
    }
}