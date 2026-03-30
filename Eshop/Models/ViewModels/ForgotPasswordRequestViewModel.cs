using System.ComponentModel.DataAnnotations;

namespace Eshop.Models.ViewModels
{
    public class ForgotPasswordRequestViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;
    }
}
