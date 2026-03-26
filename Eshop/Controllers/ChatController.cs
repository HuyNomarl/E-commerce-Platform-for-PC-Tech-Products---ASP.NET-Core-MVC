using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eshop.Models;
using System.Security.Claims;

namespace Eshop.Controllers
{
    [Authorize(Policy = Eshop.Constants.PolicyNames.CustomerSelfService)]
    public class ChatController : Controller
    {
        private readonly DataContext _context;
        private readonly UserManager<AppUserModel> _userManager;

        public ChatController(DataContext context, UserManager<AppUserModel> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetAdminInfo()
        {
            var supportUser = await GetPreferredSupportUserAsync();

            if (supportUser == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Chưa có tài khoản hỗ trợ."
                });
            }

            return Json(new
            {
                success = true,
                adminId = supportUser.Id,
                adminName = supportUser.UserName
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetSupportMessages(string adminId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(currentUserId))
                return Unauthorized();

            if (!await UserHasAnyRoleAsync(adminId, new[]
            {
                Eshop.Constants.RoleNames.SupportStaff,
                Eshop.Constants.RoleNames.Admin
            }))
            {
                return NotFound();
            }

            var messages = await _context.Messages
                .Where(x =>
                    (x.SenderId == currentUserId && x.ReceiverId == adminId) ||
                    (x.SenderId == adminId && x.ReceiverId == currentUserId))
                .OrderBy(x => x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.SenderId,
                    x.ReceiverId,
                    x.Content,
                    CreatedAt = x.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return Json(messages);
        }

        private async Task<AppUserModel?> GetPreferredSupportUserAsync()
        {
            foreach (var roleName in new[]
            {
                Eshop.Constants.RoleNames.SupportStaff,
                Eshop.Constants.RoleNames.Admin
            })
            {
                var users = await _userManager.GetUsersInRoleAsync(roleName);
                var supportUser = users
                    .OrderBy(x => x.UserName)
                    .ThenBy(x => x.Email)
                    .FirstOrDefault();

                if (supportUser != null)
                {
                    return supportUser;
                }
            }

            return null;
        }

        private async Task<bool> UserHasAnyRoleAsync(string userId, IEnumerable<string> supportedRoles)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            var roles = await _userManager.GetRolesAsync(user);
            return roles.Any(role => supportedRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }
    }
}
