using System.ComponentModel.DataAnnotations;

namespace Eshop.Models.ViewModel
{
    public class CheckoutInputViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn tỉnh/thành")]
        public string tinh { get; set; }

        public string? quan { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phường/xã")]
        public string phuong { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn mã tỉnh/thành")]
        public string ProvinceCode { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn mã phường/xã")]
        public string WardCode { get; set; }

        public string? Note { get; set; }

        public string? PaymentMethod { get; set; }
    }
}
