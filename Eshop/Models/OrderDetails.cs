using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class OrderDetails
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public OrderModel Order { get; set; } = null!;

        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public ProductModel Product { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int Quantity { get; set; }

        // Snapshot dữ liệu sản phẩm tại thời điểm mua
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }

        // Build PC
        public string? BuildGroupKey { get; set; }
        public int? PcBuildId { get; set; }
        public string? BuildName { get; set; }
        public string? ComponentType { get; set; }

        [NotMapped]
        public decimal LineTotal => Price * Quantity;
    }
}