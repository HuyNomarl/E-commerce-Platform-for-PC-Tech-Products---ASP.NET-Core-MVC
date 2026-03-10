namespace Eshop.Models.ViewModel
{
    public class CartItemViewModel
    {
        // Khởi tạo List rỗng mặc định để tránh lỗi Null
        public List<CartItemModel> CartItems { get; set; } = new List<CartItemModel>();
        public decimal GrandTotal { get; set; }
        public decimal ShippingPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? CouponCode { get; set; }

        public decimal FinalTotal { get; set; }
    }
}