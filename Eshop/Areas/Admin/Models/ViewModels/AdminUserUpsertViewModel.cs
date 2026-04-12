using System.ComponentModel.DataAnnotations;

namespace Eshop.Areas.Admin.Models.ViewModels
{
    public class AdminUserUpsertViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên tài khoản.")]
        [StringLength(100, ErrorMessage = "Tên tài khoản không được vượt quá 100 ký tự.")]
        [Display(Name = "Tên tài khoản")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(256, ErrorMessage = "Email không được vượt quá 256 ký tự.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(250, ErrorMessage = "Địa chỉ không được vượt quá 250 ký tự.")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; } = string.Empty;

        [StringLength(150, ErrorMessage = "Nghề nghiệp không được vượt quá 150 ký tự.")]
        [Display(Name = "Nghề nghiệp")]
        public string Occupation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
        [Display(Name = "Vai trò")]
        public string RoleId { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
        [StringLength(100, ErrorMessage = "Mật khẩu không được vượt quá 100 ký tự.")]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Xác nhận mật khẩu không khớp.")]
        [Display(Name = "Xác nhận mật khẩu")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public bool IsBackOfficeUser { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public bool RequiresTwoFactorEnrollment { get; set; }

        public bool IsEditMode => !string.IsNullOrWhiteSpace(Id);
    }
}
