using Microsoft.AspNetCore.Identity;

namespace Eshop.Models
{
    public class AppUserModel : IdentityUser
    {
        public string? Address { get; set; }
        public string? Occupation { get; set; }
        public string? RoleId { get; set; }
        public string? Token { get; set; }
    }
}