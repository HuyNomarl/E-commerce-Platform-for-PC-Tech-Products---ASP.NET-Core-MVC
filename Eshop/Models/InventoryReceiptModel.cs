using Eshop.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Eshop.Models
{
    public class InventoryReceiptModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string ReceiptCode { get; set; } = string.Empty;

        public int WarehouseId { get; set; }
        public int PublisherId { get; set; }

        [StringLength(100)]
        public string? ReferenceCode { get; set; }

        [StringLength(1000)]
        public string? Note { get; set; }

        public InventoryReceiptStatus Status { get; set; } = InventoryReceiptStatus.Pending;

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(450)]
        public string? ApprovedByUserId { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [StringLength(450)]
        public string? CancelledByUserId { get; set; }

        public DateTime? CancelledAt { get; set; }

        public WarehouseModel Warehouse { get; set; } = null!;
        public PublisherModel Publisher { get; set; } = null!;
        public ICollection<InventoryReceiptDetailModel> Details { get; set; } = new List<InventoryReceiptDetailModel>();
    }
}
