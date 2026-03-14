using Eshop.Repository.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class ProductModel
    {
        [Key]
        public int Id { get; set; }

        [Required, MinLength(4, ErrorMessage = "Yêu cầu nhập tên sản phẩm")]
        public string Name { get; set; }

        public string Slug { get; set; }

        [Required, MinLength(4, ErrorMessage = "Yêu cầu nhập mô tả sản phẩm")]
        public string Description { get; set; }

        public int Quantity { get; set; }
        public int Sold { get; set; }

        [Required(ErrorMessage = "Yêu cầu nhập giá sản phẩm!")]
        [Range(0.01, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [ForeignKey("PublisherId")]
        [Required, Range(1, int.MaxValue, ErrorMessage = "Yêu cầu chọn thương hiệu!")]
        public int PublisherId { get; set; }

        [ForeignKey("CategoryId")]
        [Required, Range(1, int.MaxValue, ErrorMessage = "Yêu cầu chọn danh mục!")]
        public int CategoryId { get; set; }

        public CategoryModel Category { get; set; }
        public PublisherModel Publisher { get; set; }

        public ICollection<RatingModel> RatingModel { get; set; } = new List<RatingModel>();

        public string? Image { get; set; }

        [NotMapped]
        [FileExtension]
        public IFormFile? ImageUpload { get; set; }

        public ICollection<ProductOptionGroupModel> OptionGroups { get; set; } = new List<ProductOptionGroupModel>();
        public ICollection<ProductQuantityModel> ProductQuantities { get; set; } = new List<ProductQuantityModel>();
    }
}