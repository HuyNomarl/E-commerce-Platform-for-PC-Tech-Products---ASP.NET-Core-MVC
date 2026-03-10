using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class CouponModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã khuyến mãi!")]
        [StringLength(50, ErrorMessage = "Mã khuyến mãi tối đa 50 ký tự.")]
        public string NameCode { get; set; }

        [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
        public DateTime DateStart { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc.")]
        public DateTime DateEnd { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá trị khuyến mãi!")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Giá trị khuyến mãi phải lớn hơn 0.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng!")]
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng không hợp lệ.")]
        public int Quantity { get; set; }

        [Required]
        public int Status { get; set; } = 1;

        // 1 = giảm theo %, 2 = giảm số tiền cố định
        [Required]
        public int DiscountType { get; set; } = 1;

        // Điều kiện đơn tối thiểu để dùng mã
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinOrderAmount { get; set; }

        // Giới hạn mức giảm tối đa, hữu ích khi DiscountType = 1
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; }
    }
}