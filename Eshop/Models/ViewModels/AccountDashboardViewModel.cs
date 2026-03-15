using System.Collections.Generic;

namespace Eshop.Models.ViewModels
{
    public class AccountDashboardViewModel
    {
        public AppUserModel User { get; set; }
        public List<OrderHistoryViewModel> Orders { get; set; } = new List<OrderHistoryViewModel>();
        public string ActiveTab { get; set; } = "home";

        public ChangePasswordViewModel ChangePassword { get; set; } = new ChangePasswordViewModel();
    }
}