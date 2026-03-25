using System.ComponentModel.DataAnnotations;

namespace Eshop.Models
{
    public class WarehouseModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã kho")]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên kho")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Address { get; set; }

        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<InventoryStockModel> InventoryStocks { get; set; } = new List<InventoryStockModel>();
        public ICollection<InventoryTransactionModel> InventoryTransactions { get; set; } = new List<InventoryTransactionModel>();
    }
}
