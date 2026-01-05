using System.ComponentModel.DataAnnotations;

namespace Eshop.Models
{
    public class UserModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập tên tài khoản!")]
        public string UserName { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập email!"), EmailAddress(ErrorMessage = "Email không hợp lệ!")]
        public string Email { get; set; }
        [DataType(DataType.Password), Required(ErrorMessage = "Vui lòng nhập mật khẩu!")]
        public string Password { get; set; }
    }
}
