using Eshop.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Eshop.Models
{
    public class InventoryTransactionModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string TransactionCode { get; set; } = string.Empty;

        public InventoryTransactionType TransactionType { get; set; }

        public int WarehouseId { get; set; }

        [StringLength(1000)]
        public string? Note { get; set; }

        [StringLength(100)]
        public string? ReferenceCode { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public WarehouseModel Warehouse { get; set; } = null!;
        public ICollection<InventoryTransactionDetailModel> Details { get; set; } = new List<InventoryTransactionDetailModel>();
    }
}
