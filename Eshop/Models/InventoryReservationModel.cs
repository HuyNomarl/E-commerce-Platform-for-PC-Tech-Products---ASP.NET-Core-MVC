using Eshop.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Eshop.Models
{
    public class InventoryReservationModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string ReservationCode { get; set; } = string.Empty;

        [StringLength(128)]
        public string? SessionId { get; set; }

        [StringLength(450)]
        public string? UserId { get; set; }

        [StringLength(30)]
        public string? PaymentMethod { get; set; }

        public InventoryReservationStatus Status { get; set; } = InventoryReservationStatus.Active;

        [StringLength(100)]
        public string? OrderCode { get; set; }

        [StringLength(1000)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? ReleasedAt { get; set; }

        public ICollection<InventoryReservationDetailModel> Details { get; set; } = new List<InventoryReservationDetailModel>();
    }
}
