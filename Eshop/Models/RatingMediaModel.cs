using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class RatingMediaModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RatingId { get; set; }

        [ForeignKey(nameof(RatingId))]
        public RatingModel Rating { get; set; } = null!;

        [Required]
        [StringLength(300)]
        public string PublicId { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Url { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ResourceType { get; set; } = "image";

        [StringLength(255)]
        public string? OriginalFileName { get; set; }

        [StringLength(120)]
        public string? ContentType { get; set; }

        public int SortOrder { get; set; } = 0;
    }
}
