using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eshop.Models;
using System.Security.Claims;

namespace Eshop.Controllers
{
    [Authorize]
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
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var admin = admins.FirstOrDefault();

            if (admin == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Chưa có tài khoản admin."
                });
            }

            return Json(new
            {
                success = true,
                adminId = admin.Id,
                adminName = admin.UserName
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetSupportMessages(string adminId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(currentUserId))
                return Unauthorized();

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
    }
}