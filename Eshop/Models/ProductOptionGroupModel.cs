using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class ProductOptionGroupModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên nhóm tùy chọn không được để trống")]
        public string Name { get; set; }   // RAM, SSD, VGA

        public bool IsRequired { get; set; } = false;
        public bool AllowMultiple { get; set; } = false;

        [ForeignKey("ProductId")]
        public int ProductId { get; set; }
        public ProductModel Product { get; set; }

        public ICollection<ProductOptionValueModel> OptionValues { get; set; } = new List<ProductOptionValueModel>();
    }
}