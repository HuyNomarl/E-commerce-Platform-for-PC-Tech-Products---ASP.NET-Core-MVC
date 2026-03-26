using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class InventoryReceiptDetailModel
    {
        public int Id { get; set; }

        public int InventoryReceiptId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? UnitCost { get; set; }

        public InventoryReceiptModel InventoryReceipt { get; set; } = null!;
        public ProductModel Product { get; set; } = null!;
    }
}
