using Microsoft.AspNetCore.Identity;

namespace Eshop.Models
{
    public class AppUserModel : IdentityUser
    {
        public string Occupation { get; set; } 
    }
}
