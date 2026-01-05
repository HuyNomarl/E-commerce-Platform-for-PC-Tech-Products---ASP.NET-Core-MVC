using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
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

        public async Task<IActionResult> Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PublisherModel publisher)
        {
            if (ModelState.IsValid)
            {
                publisher.Slug = publisher.Name.ToLower().Replace(" ", "-");
                var Slug = await _dataContext.Categories.FirstOrDefaultAsync(p => p.Slug == publisher.Slug);
                if (Slug != null)
                {
                    ModelState.AddModelError("", "Thương hiệu đã tồn tại!");
                    return View(publisher);
                }

                _dataContext.Add(publisher);
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Thêm thương hiệu thành công!";
                return RedirectToAction("Index");
                //await _dataContext.SaveChangesAsync();
            }
            else
            {
                TempData["error"] = "Model đang lỗi, xin thử lại sau!";
                List<string> errors = new List<string>();
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        errors.Add(error.ErrorMessage);
                    }
                }
                string errorMessages = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return BadRequest(errorMessages);
            }
            return View(publisher);
        }
        public async Task<IActionResult> Delete(int Id)
        {
            PublisherModel publisher = await _dataContext.Publishers.FindAsync(Id);

            _dataContext.Publishers.Remove(publisher);
            await _dataContext.SaveChangesAsync();
            TempData["success"] = "Xóa thương hiệu thành công!";
            return RedirectToAction("Index");

        }
        public async Task<IActionResult> Edit(int Id)
        {
            PublisherModel publisher = await _dataContext.Publishers.FindAsync(Id);

            return View(publisher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PublisherModel publisher)
        {
            // Kiểm tra xem category.Id có tồn tại không (phòng trường hợp edit mà không có Id)
            if (publisher.Id == 0)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                // Thu thập lỗi validation để trả về view (giữ lại dữ liệu người dùng nhập)
                TempData["error"] = "Dữ liệu không hợp lệ, vui lòng kiểm tra lại.";
                return View(publisher);
            }

            try
            {
                // Tạo slug từ tên danh mục
                var newSlug = publisher.Name.ToLowerInvariant().Replace(" ", "-").Trim('-');

                // Kiểm tra slug đã tồn tại chưa, nhưng loại trừ chính bản ghi đang edit
                var existingPublisherWithSlug = await _dataContext.Categories
                    .FirstOrDefaultAsync(c => c.Slug == newSlug && c.Id != publisher.Id);

                if (existingPublisherWithSlug != null)
                {
                    ModelState.AddModelError("Name", "Thương hiệu với tên này đã tồn tại (slug trùng).");
                    return View(publisher);
                }

                // Gắn slug mới
                publisher.Slug = newSlug;

                // Cách tốt hơn: Attach entity và set trạng thái Modified
                // Hoặc dùng Update nếu chắc chắn entity không bị track trước đó
                _dataContext.Publishers.Update(publisher);
                // Hoặc nếu muốn an toàn hơn:
                // _dataContext.Attach(category).State = EntityState.Modified;

                await _dataContext.SaveChangesAsync();

                TempData["success"] = "Cập nhật thương hiệu thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                // Xử lý trường hợp có người khác sửa cùng lúc
                if (!await PublisherExists(publisher.Id))
                {
                    return NotFound();
                }

                ModelState.AddModelError("", "Danh mục đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang và thử lại.");
                return View(publisher);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật danh mục. Vui lòng thử lại sau.");
                return View(publisher);
            }

        }
        private async Task<bool> PublisherExists(int id)
        {
            return await _dataContext.Categories.AnyAsync(e => e.Id == id);
        }
    }
}
