using Eshop.Constants;
using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PolicyNames.SupportManagement)]
    public class ReviewController : Controller
    {
        private readonly DataContext _context;
        private readonly UserManager<AppUserModel> _userManager;

        public ReviewController(
            DataContext context,
            UserManager<AppUserModel> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var reviews = await _context.RatingModels
                .AsNoTracking()
                .Include(x => x.Product)
                .Include(x => x.MediaItems)
                .OrderByDescending(x => x.AdminReplyAt ?? x.UpdatedAt ?? x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

            return View(reviews);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(int id, string? adminReply)
        {
            var review = await _context.RatingModels.FirstOrDefaultAsync(x => x.Id == id);
            if (review == null)
            {
                TempData["error"] = "Khong tim thay danh gia can phan hoi.";
                return RedirectToAction(nameof(Index));
            }

            var normalizedReply = string.IsNullOrWhiteSpace(adminReply) ? null : adminReply.Trim();
            if (string.IsNullOrWhiteSpace(normalizedReply))
            {
                if (!string.IsNullOrWhiteSpace(review.AdminReply))
                {
                    review.AdminReply = null;
                    review.AdminReplyAt = null;
                    review.AdminReplyByName = null;
                    review.AdminReplyUserId = null;
                    await _context.SaveChangesAsync();
                    TempData["success"] = "Da xoa phan hoi cua admin.";
                }
                else
                {
                    TempData["error"] = "Vui long nhap noi dung phan hoi.";
                }

                return RedirectToAction(nameof(Index));
            }

            if (normalizedReply.Length > 2000)
            {
                TempData["error"] = "Phan hoi toi da 2000 ky tu.";
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            review.AdminReply = normalizedReply;
            review.AdminReplyAt = DateTime.Now;
            review.AdminReplyUserId = currentUser?.Id;
            review.AdminReplyByName = string.IsNullOrWhiteSpace(currentUser?.UserName)
                ? currentUser?.Email ?? "Admin"
                : currentUser.UserName;

            await _context.SaveChangesAsync();
            TempData["success"] = "Da luu phan hoi danh gia.";
            return RedirectToAction(nameof(Index));
        }
    }
}
