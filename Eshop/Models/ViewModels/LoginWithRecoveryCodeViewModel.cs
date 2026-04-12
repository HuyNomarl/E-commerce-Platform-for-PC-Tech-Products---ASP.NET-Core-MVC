using System.ComponentModel.DataAnnotations;

namespace Eshop.Models.ViewModels
{
    public class LoginWithRecoveryCodeViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập recovery code.")]
        [Display(Name = "Recovery code")]
        public string RecoveryCode { get; set; } = string.Empty;

        public string ReturnURL { get; set; } = string.Empty;
    }
}
