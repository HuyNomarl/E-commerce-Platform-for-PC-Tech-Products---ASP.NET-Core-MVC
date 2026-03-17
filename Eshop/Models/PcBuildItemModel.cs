using Eshop.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class PcBuildItemModel
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("PcBuildId")]
        public int PcBuildId { get; set; }
        public PcBuildModel PcBuild { get; set; }

        public PcComponentType ComponentType { get; set; }

        [ForeignKey("ProductId")]
        public int ProductId { get; set; }
        public ProductModel Product { get; set; }

        public int Quantity { get; set; } = 1;
    }
}