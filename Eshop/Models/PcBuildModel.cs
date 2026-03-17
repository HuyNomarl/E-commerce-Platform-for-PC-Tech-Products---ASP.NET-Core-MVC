using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class PcBuildModel
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public string BuildCode { get; set; } = Guid.NewGuid().ToString("N")[..10].ToUpper();

        [MaxLength(200)]
        public string BuildName { get; set; } = "Cấu hình mới";

        public string? UserId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PcBuildItemModel> Items { get; set; } = new List<PcBuildItemModel>();
    }
}