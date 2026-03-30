namespace Eshop.Models.ViewModel
{
    public class CheckoutPricingSummaryViewModel
    {
        public List<CartItemModel> CartItems { get; set; } = new();
        public decimal SubTotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? CouponCode { get; set; }
        public int? CouponId { get; set; }
        public CheckoutShippingSelectionViewModel? ShippingSelection { get; set; }
    }

    public class CheckoutShippingSelectionViewModel
    {
        public string ProvinceCode { get; set; } = string.Empty;
        public string WardCode { get; set; } = string.Empty;
        public string? ProvinceName { get; set; }
        public string? DistrictName { get; set; }
        public string? WardName { get; set; }
    }

    public class PendingCheckoutStateViewModel
    {
        public string ReservationCode { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public CheckoutInputViewModel CheckoutInfo { get; set; } = new();
        public List<CartItemModel> CartItems { get; set; } = new();
        public decimal ExpectedTotal { get; set; }
        public decimal SubTotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? CouponCode { get; set; }
        public int? CouponId { get; set; }
    }
}
