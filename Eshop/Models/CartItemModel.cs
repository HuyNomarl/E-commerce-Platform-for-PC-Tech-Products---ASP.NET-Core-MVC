using System.Security.Cryptography;
using System.Text;

namespace Eshop.Models
{
    public class CartItemModel
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }

        // Gia base san pham
        public decimal BasePrice { get; set; }

        // Tong tien cong them tu option
        public decimal OptionPrice { get; set; }

        // Gia cuoi cua 1 san pham = base + option
        public decimal Price => BasePrice + OptionPrice;

        public decimal Total => Quantity * Price;

        public string? Image { get; set; }

        public List<CartItemOptionModel> SelectedOptions { get; set; } = new();

        public CartItemModel() { }

        public string? BuildGroupKey { get; set; }
        public int? PcBuildId { get; set; }
        public string? BuildName { get; set; }
        public bool IsPcBuildItem { get; set; } = false;
        public string? ComponentType { get; set; }

        public string LineKey => GenerateLineKey(ProductId, BuildGroupKey, PcBuildId, ComponentType, SelectedOptions);

        public CartItemModel(ProductModel product)
        {
            ProductId = product.Id;
            ProductName = product.Name;
            BasePrice = product.Price;
            OptionPrice = 0;
            Quantity = 1;
            Image = product.Image;
        }

        public static string GenerateLineKey(
            long productId,
            string? buildGroupKey,
            int? pcBuildId,
            string? componentType,
            IEnumerable<CartItemOptionModel>? selectedOptions)
        {
            var optionKey = string.Join("|", (selectedOptions ?? Enumerable.Empty<CartItemOptionModel>())
                .OrderBy(x => x.OptionGroupId)
                .ThenBy(x => x.OptionValueId)
                .Select(x => $"{x.OptionGroupId}:{x.OptionValueId}"));

            var rawKey = $"{productId}|{buildGroupKey}|{pcBuildId}|{componentType}|{optionKey}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
