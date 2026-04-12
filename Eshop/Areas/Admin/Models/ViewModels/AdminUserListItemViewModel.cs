namespace Eshop.Areas.Admin.Models.ViewModels
{
    public class AdminUserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string RoleDescription { get; set; } = string.Empty;
        public string AuthMethods { get; set; } = "Local";
        public bool HasPassword { get; set; }
        public bool IsLocked { get; set; }
        public string? LockoutEndText { get; set; }
        public bool IsCurrentUser { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public bool RequiresTwoFactorEnrollment { get; set; }
    }
}
