using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Eshop.Models
{
    public class CategoryModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Yêu cầu nhập tên danh mục")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Yêu cầu nhập mô tả danh mục")]
        public string Description { get; set; }

        public string Slug { get; set; }
        public int Status { get; set; }

        public int? ParentCategoryId { get; set; }

        [ForeignKey("ParentCategoryId")]
        [JsonIgnore]
        public CategoryModel? ParentCategory { get; set; }

        [JsonIgnore]
        public ICollection<CategoryModel> Children { get; set; } = new List<CategoryModel>();

        [JsonIgnore]
        public ICollection<ProductModel> Products { get; set; } = new List<ProductModel>();
    }
}