using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class ProductImageModel
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

        public bool IsMain { get; set; } = false;

        public int SortOrder { get; set; } = 0;
    }
}