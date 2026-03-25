using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class InventoryStockModel
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public int WarehouseId { get; set; }

        public int OnHandQuantity { get; set; } = 0;
        public int ReservedQuantity { get; set; } = 0;

        [NotMapped]
        public int AvailableQuantity => OnHandQuantity - ReservedQuantity;

        public ProductModel Product { get; set; } = null!;
        public WarehouseModel Warehouse { get; set; } = null!;
    }
}
