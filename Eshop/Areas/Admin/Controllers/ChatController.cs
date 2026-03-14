using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ChatController : Controller
    {
        private readonly DataContext _context;

        public ChatController(DataContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetUsersChatted()
        {
            var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var userIds = await _context.Messages
                .Where(x => x.SenderId == adminId || x.ReceiverId == adminId)
                .Select(x => x.SenderId == adminId ? x.ReceiverId : x.SenderId)
                .Distinct()
                .ToListAsync();

            var users = await _context.Users
                .Where(x => userIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.UserName,
                    x.Email
                })
                .ToListAsync();

            return Json(users);
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(string userId)
        {
            var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var messages = await _context.Messages
                .Where(x =>
                    (x.SenderId == adminId && x.ReceiverId == userId) ||
                    (x.SenderId == userId && x.ReceiverId == adminId))
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