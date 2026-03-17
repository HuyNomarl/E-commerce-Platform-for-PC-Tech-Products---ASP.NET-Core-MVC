using Eshop.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Eshop.Models
{
    public class SpecificationDefinitionModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Code { get; set; }   // ví dụ: cpu_socket, ram_type, gpu_vram_gb

        [Required]
        public string Name { get; set; }   // Socket CPU, Loại RAM, VRAM...

        public SpecificationDataType DataType { get; set; } = SpecificationDataType.Text;

        public string? Unit { get; set; }

        public PcComponentType? ComponentType { get; set; }

        public bool IsRequired { get; set; } = false;
        public bool IsFilterable { get; set; } = true;
        public int SortOrder { get; set; } = 0;

        public ICollection<ProductSpecificationModel> ProductSpecifications { get; set; } = new List<ProductSpecificationModel>();
    }
}