using System.ComponentModel.DataAnnotations;

namespace Eshop.Models.ViewModels
{
    public class LoginViewModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập tên tài khoản!")]
        public string UserName { get; set; }

        [DataType(DataType.Password), Required(ErrorMessage = "Vui lòng nhập mật khẩu!")]
        public string Password { get; set; }
        public string ReturnURL { get; set; }
    }
}
