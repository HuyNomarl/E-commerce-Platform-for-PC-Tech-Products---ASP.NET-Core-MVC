using Eshop.Models;
using Eshop.Repository;
using Eshop.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PolicyNames.CatalogManagement)]
    public class CategoryController : Controller
    {
        private readonly DataContext _dataContext;

        public CategoryController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<IActionResult> Index(int pg = 1)
        {
            List<CategoryModel> categories = await _dataContext.Categories
                .Include(c => c.ParentCategory)
                .OrderBy(c => c.ParentCategoryId)
                .ThenBy(c => c.Id)
                .ToListAsync();

            const int pageSize = 10;

            if (pg < 1)
                pg = 1;

            int recsCount = categories.Count();
            var pager = new Paginate(recsCount, pg, pageSize);

            int recSkip = (pg - 1) * pageSize;

            var data = categories.Skip(recSkip).Take(pager.PageSize).ToList();

            ViewBag.Pager = pager;

            return View(data);
        }

        public async Task<IActionResult> Create()
        {
            await LoadParentCategories();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryModel category)
        {
            await LoadParentCategories(category.ParentCategoryId);

            if (!ModelState.IsValid)
            {
                TempData["error"] = "Dữ liệu không hợp lệ, vui lòng kiểm tra lại.";
                return View(category);
            }

            // Tạo slug
            category.Slug = category.Name.ToLowerInvariant().Trim().Replace(" ", "-");

            // Kiểm tra slug trùng
            var slugExists = await _dataContext.Categories
                .AnyAsync(c => c.Slug == category.Slug);

            if (slugExists)
            {
                ModelState.AddModelError("Name", "Danh mục đã tồn tại.");
                return View(category);
            }

            // Nếu có chọn danh mục cha thì kiểm tra có tồn tại không
            if (category.ParentCategoryId.HasValue)
            {
                var parentExists = await _dataContext.Categories
                    .AnyAsync(c => c.Id == category.ParentCategoryId.Value);

                if (!parentExists)
                {
                    ModelState.AddModelError("ParentCategoryId", "Danh mục cha không tồn tại.");
                    return View(category);
                }
            }

            _dataContext.Categories.Add(category);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Thêm danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var category = await _dataContext.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            await LoadParentCategories(category.ParentCategoryId, category.Id);
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CategoryModel category)
        {
            if (category.Id == 0)
            {
                return NotFound();
            }

            await LoadParentCategories(category.ParentCategoryId, category.Id);

            if (!ModelState.IsValid)
            {
                TempData["error"] = "Dữ liệu không hợp lệ, vui lòng kiểm tra lại.";
                return View(category);
            }

            var existingCategory = await _dataContext.Categories
                .FirstOrDefaultAsync(c => c.Id == category.Id);

            if (existingCategory == null)
            {
                return NotFound();
            }

            // Không cho chính nó làm cha của chính nó
            if (category.ParentCategoryId == category.Id)
            {
                ModelState.AddModelError("ParentCategoryId", "Không thể chọn chính danh mục này làm danh mục cha.");
                return View(category);
            }

            // Kiểm tra parent có tồn tại không
            if (category.ParentCategoryId.HasValue)
            {
                var parentExists = await _dataContext.Categories
                    .AnyAsync(c => c.Id == category.ParentCategoryId.Value && c.Id != category.Id);

                if (!parentExists)
                {
                    ModelState.AddModelError("ParentCategoryId", "Danh mục cha không tồn tại.");
                    return View(category);
                }
            }

            // Tạo slug mới
            var newSlug = category.Name.ToLowerInvariant().Trim().Replace(" ", "-");

            var slugExists = await _dataContext.Categories
                .AnyAsync(c => c.Slug == newSlug && c.Id != category.Id);

            if (slugExists)
            {
                ModelState.AddModelError("Name", "Danh mục với tên này đã tồn tại.");
                return View(category);
            }

            // Cập nhật dữ liệu
            existingCategory.Name = category.Name;
            existingCategory.Description = category.Description;
            existingCategory.Status = category.Status;
            existingCategory.ParentCategoryId = category.ParentCategoryId;
            existingCategory.Slug = newSlug;

            try
            {
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Cập nhật danh mục thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await CategoryExists(category.Id))
                {
                    return NotFound();
                }

                ModelState.AddModelError("", "Danh mục đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang và thử lại.");
                return View(category);
            }
            catch
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật danh mục. Vui lòng thử lại sau.");
                return View(category);
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            var category = await _dataContext.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            _dataContext.Categories.Remove(category);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Xóa danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> CategoryExists(int id)
        {
            return await _dataContext.Categories.AnyAsync(e => e.Id == id);
        }

        private async Task LoadParentCategories(int? selectedId = null, int? currentCategoryId = null)
        {
            var categories = await _dataContext.Categories
                .Where(c => !currentCategoryId.HasValue || c.Id != currentCategoryId.Value)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.ParentCategories = new SelectList(categories, "Id", "Name", selectedId);
        }
    }
}
