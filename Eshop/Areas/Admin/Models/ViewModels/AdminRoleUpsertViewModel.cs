using System.ComponentModel.DataAnnotations;

namespace Eshop.Areas.Admin.Models.ViewModels
{
    public class AdminRoleUpsertViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên vai trò.")]
        [StringLength(100, ErrorMessage = "Tên vai trò không được vượt quá 100 ký tự.")]
        [Display(Name = "Tên vai trò")]
        public string Name { get; set; } = string.Empty;

        public bool IsEditMode => !string.IsNullOrWhiteSpace(Id);
    }
}
