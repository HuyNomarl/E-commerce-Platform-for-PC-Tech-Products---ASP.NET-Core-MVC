using System.ComponentModel.DataAnnotations;

namespace Eshop.Models.ViewModels
{
    public class LoginWith2faViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mã xác thực.")]
        [Display(Name = "Mã xác thực")]
        public string TwoFactorCode { get; set; } = string.Empty;

        public string ReturnURL { get; set; } = string.Empty;

        [Display(Name = "Ghi nhớ thiết bị này")]
        public bool RememberMachine { get; set; }
    }
}
