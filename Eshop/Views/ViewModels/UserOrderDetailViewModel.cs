using Microsoft.AspNetCore.Mvc.Rendering;

namespace Eshop.Models.ViewModels
{
    public class UserOrderDetailViewModel
    {
        public string OrderCode { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public int Status { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Note { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }
        public bool CanCancel { get; set; }

        public List<UserOrderItemViewModel> OrderDetails { get; set; } = new();
    }

    public class UserOrderItemViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? BuildName { get; set; }
        public string? ComponentType { get; set; }
        public decimal LineTotal => Price * Quantity;
    }
}
