using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class PcBuildShareModel
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(16)]
        public string ShareCode { get; set; } = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        [ForeignKey(nameof(PcBuildId))]
        public int PcBuildId { get; set; }
        public PcBuildModel PcBuild { get; set; }

        [Required]
        [MaxLength(450)]
        public string SenderUserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(450)]
        public string ReceiverUserId { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? OpenedAt { get; set; }

        public AppUserModel SenderUser { get; set; }
        public AppUserModel ReceiverUser { get; set; }
    }
}
