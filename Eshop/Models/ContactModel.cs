using Eshop.Repository.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eshop.Models
{
    public class ContactModel
    {
        [Key]
        [Required(ErrorMessage = "Yêu cầu nhập tiêu đề!")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Yêu cầu nhập địa chỉ!")]
        public string Map { get; set; }
        [Required(ErrorMessage = "Yêu cầu nhập số điện thoại!")]
        public string Phone { get; set; }
        [Required(ErrorMessage = "Yêu cầu nhập Email!")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Yêu cầu nhập mô tả!")]
        public string Description { get; set; }
        public string LogoImg { get; set; }
        [NotMapped]
        [FileExtension]
        public IFormFile LogoImgFile { get; set; }
    }
}
