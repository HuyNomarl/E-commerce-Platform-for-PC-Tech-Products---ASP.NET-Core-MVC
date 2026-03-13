namespace Eshop.Models.ViewModels
{
    public class OrderHistoryViewModel
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public DateTime CreatedTime { get; set; }
        public int Status { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }
    }
}