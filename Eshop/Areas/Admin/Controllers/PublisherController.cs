using Eshop.Constants;
using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PolicyNames.BrandManagement)]
    public class PublisherController : Controller
    {
        private readonly DataContext _dataContext;

        public PublisherController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _dataContext.Publishers.OrderByDescending(p => p.Id).ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PublisherModel publisher)
        {
            if (!ModelState.IsValid)
            {
                TempData["error"] = "Dữ liệu không hợp lệ, vui lòng kiểm tra lại.";
                return View(publisher);
            }

            publisher.Slug = BuildSlug(publisher.Name);
            var existingPublisher = await _dataContext.Publishers.FirstOrDefaultAsync(p => p.Slug == publisher.Slug);
            if (existingPublisher != null)
            {
                ModelState.AddModelError("Name", "Thương hiệu đã tồn tại.");
                return View(publisher);
            }

            _dataContext.Publishers.Add(publisher);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Thêm thương hiệu thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var publisher = await _dataContext.Publishers.FindAsync(id);
            if (publisher == null)
            {
                return NotFound();
            }

            _dataContext.Publishers.Remove(publisher);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Xóa thương hiệu thành công!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var publisher = await _dataContext.Publishers.FindAsync(id);
            if (publisher == null)
            {
                return NotFound();
            }

            return View(publisher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PublisherModel publisher)
        {
            if (publisher.Id == 0)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData["error"] = "Dữ liệu không hợp lệ, vui lòng kiểm tra lại.";
                return View(publisher);
            }

            var existingPublisher = await _dataContext.Publishers.FirstOrDefaultAsync(x => x.Id == publisher.Id);
            if (existingPublisher == null)
            {
                return NotFound();
            }

            var newSlug = BuildSlug(publisher.Name);
            var slugExists = await _dataContext.Publishers.AnyAsync(x => x.Slug == newSlug && x.Id != publisher.Id);
            if (slugExists)
            {
                ModelState.AddModelError("Name", "Thương hiệu với tên này đã tồn tại.");
                return View(publisher);
            }

            existingPublisher.Name = publisher.Name;
            existingPublisher.Description = publisher.Description;
            existingPublisher.Slug = newSlug;
            existingPublisher.status = publisher.status;

            try
            {
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Cập nhật thương hiệu thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await PublisherExists(publisher.Id))
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, "Thương hiệu đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang và thử lại.");
                return View(publisher);
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi cập nhật thương hiệu. Vui lòng thử lại sau.");
                return View(publisher);
            }
        }

        private async Task<bool> PublisherExists(int id)
        {
            return await _dataContext.Publishers.AnyAsync(e => e.Id == id);
        }

        private static string BuildSlug(string? name)
        {
            return (name ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", "-").Trim('-');
        }
    }
}
