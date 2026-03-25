using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class InventoryTransactionDetailModel
    {
        public int Id { get; set; }

        public int InventoryTransactionId { get; set; }
        public int ProductId { get; set; }

        public int Quantity { get; set; }
        public int BeforeQuantity { get; set; }
        public int AfterQuantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? UnitCost { get; set; }

        public InventoryTransactionModel InventoryTransaction { get; set; } = null!;
        public ProductModel Product { get; set; } = null!;
    }
}
