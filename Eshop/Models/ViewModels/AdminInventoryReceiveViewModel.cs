using Eshop.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Eshop.Models.ViewModels
{
    public class AdminInventoryReceiveViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn kho")]
        public int WarehouseId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn brand")]
        public int PublisherId { get; set; }

        [StringLength(100)]
        public string? ReferenceCode { get; set; }

        [StringLength(1000)]
        public string? Note { get; set; }

        public List<SelectListItem> Warehouses { get; set; } = new();
        public List<SelectListItem> Products { get; set; } = new();
        public List<SelectListItem> Publishers { get; set; } = new();
        public List<AdminInventoryReceiveProductOptionViewModel> ProductOptions { get; set; } = new();
        public List<AdminInventoryReceiptSummaryViewModel> RecentReceipts { get; set; } = new();

        public List<AdminInventoryReceiveItemViewModel> Items { get; set; } = new()
        {
            new AdminInventoryReceiveItemViewModel()
        };
    }

    public class AdminInventoryReceiveItemViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn sản phẩm")]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public int Quantity { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá nhập không hợp lệ")]
        public decimal? UnitCost { get; set; }
    }

    public class AdminInventoryReceiveProductOptionViewModel
    {
        public int ProductId { get; set; }
        public int PublisherId { get; set; }
        public string ProductName { get; set; } = string.Empty;
    }

    public class AdminInventoryReceiptSummaryViewModel
    {
        public int Id { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string PublisherName { get; set; } = string.Empty;
        public string? ReferenceCode { get; set; }
        public string? Note { get; set; }
        public InventoryReceiptStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public List<AdminInventoryReceiptItemSummaryViewModel> Items { get; set; } = new();
    }

    public class AdminInventoryReceiptItemSummaryViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal? UnitCost { get; set; }
    }

    public class AdminInventoryAdjustViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn kho")]
        public int WarehouseId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn sản phẩm")]
        public int ProductId { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng mới không hợp lệ")]
        public int NewQuantity { get; set; }

        [StringLength(100)]
        public string? ReferenceCode { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lý do điều chỉnh")]
        [StringLength(1000)]
        public string? Note { get; set; }

        public List<SelectListItem> Warehouses { get; set; } = new();
        public List<SelectListItem> Products { get; set; } = new();
    }

    public class AdminInventoryTransferViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn kho nguồn")]
        public int FromWarehouseId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn kho đích")]
        public int ToWarehouseId { get; set; }

        [StringLength(100)]
        public string? ReferenceCode { get; set; }

        [StringLength(1000)]
        public string? Note { get; set; }

        public List<SelectListItem> Warehouses { get; set; } = new();
        public List<SelectListItem> Products { get; set; } = new();

        public List<AdminInventoryTransferItemViewModel> Items { get; set; } = new()
        {
            new AdminInventoryTransferItemViewModel()
        };
    }

    public class AdminInventoryTransferItemViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn sản phẩm")]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public int Quantity { get; set; }
    }
}
