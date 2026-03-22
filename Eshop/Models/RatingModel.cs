using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class RatingModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Name { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string? Email { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        [Range(1, 5)]
        public int Stars { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(ProductId))]
        public ProductModel Product { get; set; } = null!;
    }
}