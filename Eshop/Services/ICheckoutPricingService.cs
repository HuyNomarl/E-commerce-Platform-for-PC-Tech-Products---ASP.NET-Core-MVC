using Eshop.Models.ViewModel;

namespace Eshop.Services
{
    public interface ICheckoutPricingService
    {
        Task<CheckoutPricingSummaryViewModel> BuildSummaryAsync(HttpContext httpContext, CheckoutInputViewModel? checkoutModel = null);
        Task<CheckoutShippingSelectionViewModel?> GetStoredShippingSelectionAsync(HttpContext httpContext);
        void SaveShippingSelection(HttpContext httpContext, CheckoutShippingSelectionViewModel selection);
        void ClearShippingSelection(HttpContext httpContext);
    }
}
