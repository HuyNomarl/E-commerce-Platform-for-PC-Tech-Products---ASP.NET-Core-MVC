using System.ComponentModel.DataAnnotations;

namespace Eshop.Models.ViewModels
{
    public class SetupAuthenticatorViewModel
    {
        public string SharedKey { get; set; } = string.Empty;
        public string AuthenticatorUri { get; set; } = string.Empty;
        public string ReturnURL { get; set; } = string.Empty;
        public bool IsSetupComplete { get; set; }
        public IReadOnlyCollection<string> RecoveryCodes { get; set; } = Array.Empty<string>();

        [Required(ErrorMessage = "Vui lòng nhập mã xác thực từ ứng dụng Authenticator.")]
        [Display(Name = "Mã xác thực")]
        public string Code { get; set; } = string.Empty;
    }
}
