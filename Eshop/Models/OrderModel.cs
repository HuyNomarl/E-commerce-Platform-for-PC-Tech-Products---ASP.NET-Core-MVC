using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class OrderModel
    {
        [Key]
        public int OrderId { get; set; }
        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string OrderCode { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        [StringLength(50)]
        public string? CouponCode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingCost { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } = 0;

        [Required]
        [StringLength(256)]
        public string UserName { get; set; } = string.Empty;

        [StringLength(150)]
        public string? FullName { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(150)]
        public string? Email { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? Province { get; set; }

        [StringLength(100)]
        public string? District { get; set; }

        [StringLength(100)]
        public string? Ward { get; set; }

        [StringLength(1000)]
        public string? Note { get; set; }

        public DateTime CreatedTime { get; set; } = DateTime.Now;

        public int Status { get; set; } = 1;

        public ICollection<OrderDetails> OrderDetails { get; set; } = new List<OrderDetails>();
    }
}