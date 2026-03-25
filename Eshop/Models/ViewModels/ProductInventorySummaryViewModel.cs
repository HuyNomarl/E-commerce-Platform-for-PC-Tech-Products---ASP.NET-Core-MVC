namespace Eshop.Models.ViewModels
{
    public class ProductInventorySummaryViewModel
    {
        public int ProductId { get; set; }
        public int TotalOnHand { get; set; }
        public int TotalReserved { get; set; }
        public int AvailableQuantity => TotalOnHand - TotalReserved;
        public string StockText => AvailableQuantity > 0 ? "Còn hàng" : "Hết hàng";
    }
}
