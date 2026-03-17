using Eshop.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class PrebuiltPcComponentModel
    {
        [Key]
        public int Id { get; set; }

        public PcComponentType ComponentType { get; set; }

        // Bộ PC dựng sẵn
        [ForeignKey("ProductId")]
        public int ProductId { get; set; }
        public ProductModel Product { get; set; }

        // Linh kiện thật
        [ForeignKey("ComponentProductId")]
        public int ComponentProductId { get; set; }
        public ProductModel ComponentProduct { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AdditionalPrice { get; set; } = 0;

        public bool IsDefault { get; set; } = true;
    }
}