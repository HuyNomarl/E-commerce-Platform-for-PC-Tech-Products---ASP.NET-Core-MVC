using Microsoft.AspNetCore.Mvc.Rendering;

namespace Eshop.Models.ViewModels
{
    public class UserOrderDetailViewModel
    {
        public string OrderCode { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedTime { get; set; }
        public int Status { get; set; }
        public decimal ShippingCost { get; set; }

        public List<UserOrderItemViewModel> OrderDetails { get; set; } = new();
    }

    public class UserOrderItemViewModel
    {
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal => Price * Quantity;
    }
}