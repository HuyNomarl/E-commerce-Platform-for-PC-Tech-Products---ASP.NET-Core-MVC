using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class ProductSpecificationModel
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("ProductId")]
        public int ProductId { get; set; }
        public ProductModel Product { get; set; }

        [ForeignKey("SpecificationDefinitionId")]
        public int SpecificationDefinitionId { get; set; }
        public SpecificationDefinitionModel SpecificationDefinition { get; set; }

        public string? ValueText { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ValueNumber { get; set; }

        public bool? ValueBool { get; set; }

        // Dùng cho các list như: ["ATX","M-ATX","ITX"]
        public string? ValueJson { get; set; }
    }
}