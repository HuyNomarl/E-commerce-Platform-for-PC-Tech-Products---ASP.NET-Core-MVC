using Microsoft.AspNetCore.Mvc.Rendering;

namespace Eshop.Models.ViewModels
{
    public class OrderDetailViewModel
    {
        public string OrderCode { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }

        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Province { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public string? Note { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }


        public int Status { get; set; }

        public List<SelectListItem> StatusList { get; set; } = new();

        public List<OrderDetails> OrderDetails { get; set; } = new();
    }
}
