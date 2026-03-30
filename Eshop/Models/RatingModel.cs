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

        [StringLength(2000)]
        public string? AdminReply { get; set; }

        [StringLength(450)]
        public string? AdminReplyUserId { get; set; }

        [StringLength(150)]
        public string? AdminReplyByName { get; set; }

        public DateTime? AdminReplyAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(ProductId))]
        public ProductModel Product { get; set; } = null!;

        public ICollection<RatingMediaModel> MediaItems { get; set; } = new List<RatingMediaModel>();
    }
}
