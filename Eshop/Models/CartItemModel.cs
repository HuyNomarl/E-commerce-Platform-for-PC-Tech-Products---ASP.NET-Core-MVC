namespace Eshop.Models
{
    public class CartItemModel
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }

        // Giá base sản phẩm
        public decimal BasePrice { get; set; }

        // Tổng tiền cộng thêm từ option
        public decimal OptionPrice { get; set; }

        // Giá cuối của 1 sản phẩm = base + option
        public decimal Price => BasePrice + OptionPrice;

        public decimal Total => Quantity * Price;

        public string Image { get; set; }

        public List<CartItemOptionModel> SelectedOptions { get; set; } = new List<CartItemOptionModel>();

        public CartItemModel() { }

        public string? BuildGroupKey { get; set; }
        public int? PcBuildId { get; set; }
        public string? BuildName { get; set; }
        public bool IsPcBuildItem { get; set; } = false;
        public string? ComponentType { get; set; }

        public CartItemModel(ProductModel product)
        {
            ProductId = product.Id;
            ProductName = product.Name;
            BasePrice = product.Price;
            OptionPrice = 0;
            Quantity = 1;
            Image = product.Image;
        }
    }
}