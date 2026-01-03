using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{

    [Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly DataContext _dataContext;
        public CategoryController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }
        public async Task<IActionResult> Index()
        {
            return View(await _dataContext.Categories.OrderByDescending(p => p.Id).ToListAsync());
        }
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryModel category)
        {
            if (ModelState.IsValid)
            {
                category.Slug = category.Name.ToLower().Replace(" ", "-");
                var Slug = await _dataContext.Categories.FirstOrDefaultAsync(p => p.Slug == category.Slug);
                if (Slug != null)
                {
                    ModelState.AddModelError("", "Danh mục đã tồn tại!");
                    return View(category);
                }

                _dataContext.Add(category);
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Thêm danh mục thành công!";
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
            return View(category);
        }
        public async Task<IActionResult> Delete(int Id)
        {
            CategoryModel category = await _dataContext.Categories.FindAsync(Id);

            _dataContext.Categories.Remove(category);
            await _dataContext.SaveChangesAsync();
            TempData["success"] = "Xóa danh mục thành công!";
            return RedirectToAction("Index");

        }
        public async Task<IActionResult> Edit(int Id)
        {
            CategoryModel category = await _dataContext.Categories.FindAsync(Id);

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CategoryModel category)
        {
            // Kiểm tra xem category.Id có tồn tại không (phòng trường hợp edit mà không có Id)
            if (category.Id == 0)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                // Thu thập lỗi validation để trả về view (giữ lại dữ liệu người dùng nhập)
                TempData["error"] = "Dữ liệu không hợp lệ, vui lòng kiểm tra lại.";
                return View(category);
            }

            try
            {
                // Tạo slug từ tên danh mục
                var newSlug = category.Name.ToLowerInvariant().Replace(" ", "-").Trim('-');

                // Kiểm tra slug đã tồn tại chưa, nhưng loại trừ chính bản ghi đang edit
                var existingCategoryWithSlug = await _dataContext.Categories
                    .FirstOrDefaultAsync(c => c.Slug == newSlug && c.Id != category.Id);

                if (existingCategoryWithSlug != null)
                {
                    ModelState.AddModelError("Name", "Danh mục với tên này đã tồn tại (slug trùng).");
                    return View(category);
                }

                // Gắn slug mới
                category.Slug = newSlug;

                // Cách tốt hơn: Attach entity và set trạng thái Modified
                // Hoặc dùng Update nếu chắc chắn entity không bị track trước đó
                _dataContext.Categories.Update(category);
                // Hoặc nếu muốn an toàn hơn:
                // _dataContext.Attach(category).State = EntityState.Modified;

                await _dataContext.SaveChangesAsync();

                TempData["success"] = "Cập nhật danh mục thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                // Xử lý trường hợp có người khác sửa cùng lúc
                if (!await CategoryExists(category.Id))
                {
                    return NotFound();
                }

                ModelState.AddModelError("", "Danh mục đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang và thử lại.");
                return View(category);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật danh mục. Vui lòng thử lại sau.");
                return View(category);
            }
        }

        private async Task<bool> CategoryExists(int id)
        {
            return await _dataContext.Categories.AnyAsync(e => e.Id == id);
        }
    }
}
