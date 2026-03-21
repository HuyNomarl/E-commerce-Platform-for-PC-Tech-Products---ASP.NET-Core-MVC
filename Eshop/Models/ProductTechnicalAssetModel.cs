using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class ProductTechnicalAssetModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public ProductModel Product { get; set; } = null!;

        [Required]
        public string PublicId { get; set; } = null!;

        [Required]
        public string Url { get; set; } = null!;

        // image / raw / video
        [Required]
        public string ResourceType { get; set; } = "raw";

        public string? OriginalFileName { get; set; }

        public string? ContentType { get; set; }
    }
}