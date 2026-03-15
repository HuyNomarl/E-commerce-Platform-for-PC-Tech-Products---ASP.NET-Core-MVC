using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class ProductOptionValueModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Giá trị tùy chọn không được để trống")]
        public string Value { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AdditionalPrice { get; set; } = 0;

        public int Stock { get; set; } = 0;
        public int Status { get; set; } = 1;
        public bool IsDefault { get; set; } = false;

        public int SortOrder { get; set; } = 0;

        [ForeignKey("ProductOptionGroupId")]
        public int ProductOptionGroupId { get; set; }

        public ProductOptionGroupModel ProductOptionGroup { get; set; }
    }
}