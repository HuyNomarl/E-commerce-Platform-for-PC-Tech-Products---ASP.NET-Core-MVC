using Eshop.Repository.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class SliderModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Không được để trống tên slider!")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Không được để trống mô tả slider!")]
        public string Description { get; set; }
        public int? Status { get; set; }
        public string Image { get; set; }
        [NotMapped]
        [FileExtension]
        [Required(ErrorMessage = "Không được để trống hình ảnh slider!")]
        public IFormFile? ImageUpload { get; set; }
    }
}
