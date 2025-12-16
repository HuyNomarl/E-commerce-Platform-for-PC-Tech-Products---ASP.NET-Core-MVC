using System.ComponentModel.DataAnnotations;

namespace Eshop.Models
{
    public class PublisherModel
    {
        [Key]
        public int Id { get; set; }
        [Required, MinLength(4, ErrorMessage = "Yêu cầu nhập tên Nhà xuất bản")]
        public string Name { get; set; }
        [Required, MinLength(4, ErrorMessage = "Yêu cầu nhập mô tả Nhà xuất bản")]
        public string Description { get; set; }
        public string Slug { get; set; }
        public int status { get; set; }
    }
}
