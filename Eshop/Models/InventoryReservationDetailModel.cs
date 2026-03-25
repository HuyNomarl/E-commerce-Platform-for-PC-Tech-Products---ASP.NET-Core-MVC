namespace Eshop.Models
{
    public class InventoryReservationDetailModel
    {
        public int Id { get; set; }

        public int InventoryReservationId { get; set; }
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }

        public int Quantity { get; set; }

        public InventoryReservationModel InventoryReservation { get; set; } = null!;
        public ProductModel Product { get; set; } = null!;
        public WarehouseModel Warehouse { get; set; } = null!;
    }
}
