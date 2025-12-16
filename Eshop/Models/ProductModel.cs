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
        [Required, MinLength(4, ErrorMessage = "Yêu cầu nhập giá sản phẩm")]
        public decimal Price { get; set; }
        [ForeignKey("PublisherId")]
        public int PublisherId { get; set; }
        [ForeignKey("CategoryId")]
        public int CategoryId { get; set; } 
        public CategoryModel Category { get; set; }
        public PublisherModel Publisher { get; set; }

        public String Image { get; set; }

    }
}
