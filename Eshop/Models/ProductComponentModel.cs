using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class ProductComponentModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ComponentType { get; set; } // CPU, Main, RAM, SSD, VGA, PSU...

        [Required]
        public string ComponentName { get; set; } // i5-14400F, B760M, RTX 4060...

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } = 0;

        public bool IsDefault { get; set; } = true;

        [ForeignKey("ProductId")]
        public int ProductId { get; set; }
        public ProductModel Product { get; set; }
    }
}