namespace Eshop.Models.ViewModel
{
    public class CartItemViewModel
    {
        // Khởi tạo List rỗng mặc định để tránh lỗi Null
        public List<CartItemModel> CartItems { get; set; } = new List<CartItemModel>();
        public decimal GrandTotal { get; set; }
    }
}