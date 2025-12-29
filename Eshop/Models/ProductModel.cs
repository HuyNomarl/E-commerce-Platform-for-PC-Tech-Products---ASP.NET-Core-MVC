using Eshop.Repository.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class ProductModel
    {
        [Key]
        public int Id { get; set; }
        [Required, MinLength(4, ErrorMessage = "Yêu cầu nhập tên Sản phẩm")]
        public string Name { get; set; }
        public string Slug { get; set; }
        [Required, MinLength(4, ErrorMessage = "Yêu cầu nhập mô tả Sản phẩm")]
        public string Description { get; set; }
        //[Required, MinLength(4, ErrorMessage = "Yêu cầu nhập giá sản phẩm")]
        [Required(ErrorMessage ="Yêu cầu nhập giá sản phẩm!")]
        [Range(0.01, double.MaxValue)]
        [Column(TypeName = "decimal(8,2)")]
        public decimal Price { get; set; }
        [ForeignKey("PublisherId")]
        [Required, Range(1, int.MaxValue, ErrorMessage = "Yêu cầu chọn thương hiệu!")]
        public int PublisherId { get; set; }
        [ForeignKey("CategoryId")]
        [Required, Range(1, int.MaxValue, ErrorMessage = "Yêu cầu chọn danh mục!")]
        public int CategoryId { get; set; } 
        public CategoryModel Category { get; set; }
        public PublisherModel Publisher { get; set; }

        public string? Image { get; set; } 

        [NotMapped]
        [FileExtension]
        public IFormFile? ImageUpload { get; set; }

    }
}
