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
    public class ProductOptionController : Controller
    {
        private readonly DataContext _dataContext;

        public ProductOptionController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<IActionResult> Index(int productId)
        {
            var product = await _dataContext.Products
                .Include(p => p.OptionGroups)
                    .ThenInclude(g => g.OptionValues)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> CreateGroup(int productId)
        {
            var product = await _dataContext.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound();
            }

            ViewBag.Product = product;
            return View(new ProductOptionGroupModel { ProductId = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup(ProductOptionGroupModel model)
        {
            var product = await _dataContext.Products.FindAsync(model.ProductId);
            if (product == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Product = product;
                return View(model);
            }

            _dataContext.ProductOptionGroups.Add(model);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Thêm nhóm tùy chọn thành công!";
            return RedirectToAction(nameof(Index), new { productId = model.ProductId });
        }

        [HttpGet]
        public async Task<IActionResult> CreateValue(int groupId)
        {
            var group = await _dataContext.ProductOptionGroups
                .Include(g => g.Product)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
            {
                return NotFound();
            }

            ViewBag.Group = group;
            return View(new ProductOptionValueModel { ProductOptionGroupId = groupId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateValue(ProductOptionValueModel model)
        {
            var group = await _dataContext.ProductOptionGroups
                .Include(g => g.Product)
                .FirstOrDefaultAsync(g => g.Id == model.ProductOptionGroupId);

            if (group == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Group = group;
                return View(model);
            }

            _dataContext.ProductOptionValues.Add(model);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Thêm giá trị tùy chọn thành công!";
            return RedirectToAction(nameof(Index), new { productId = group.ProductId });
        }

        [HttpGet]
        public async Task<IActionResult> EditGroup(int id)
        {
            var group = await _dataContext.ProductOptionGroups.FindAsync(id);
            if (group == null)
            {
                return NotFound();
            }

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGroup(int id, ProductOptionGroupModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var group = await _dataContext.ProductOptionGroups.FindAsync(id);
            if (group == null)
            {
                return NotFound();
            }

            group.Name = model.Name;
            group.IsRequired = model.IsRequired;
            group.AllowMultiple = model.AllowMultiple;

            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Cập nhật nhóm tùy chọn thành công!";
            return RedirectToAction(nameof(Index), new { productId = group.ProductId });
        }

        [HttpGet]
        public async Task<IActionResult> EditValue(int id)
        {
            var value = await _dataContext.ProductOptionValues.FindAsync(id);
            if (value == null)
            {
                return NotFound();
            }

            return View(value);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditValue(int id, ProductOptionValueModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var value = await _dataContext.ProductOptionValues.FindAsync(id);
            if (value == null)
            {
                return NotFound();
            }

            value.Value = model.Value;
            value.AdditionalPrice = model.AdditionalPrice;
            value.Stock = model.Stock;
            value.Status = model.Status;
            value.IsDefault = model.IsDefault;
            value.SortOrder = model.SortOrder;

            await _dataContext.SaveChangesAsync();

            return RedirectToAction("Index", new { productId = 1 });
        }
        public async Task<IActionResult> DeleteGroup(int id)
        {
            var group = await _dataContext.ProductOptionGroups
                .Include(g => g.OptionValues)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null)
            {
                return NotFound();
            }

            int productId = group.ProductId;

            _dataContext.ProductOptionValues.RemoveRange(group.OptionValues);
            _dataContext.ProductOptionGroups.Remove(group);

            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Xóa nhóm tùy chọn thành công!";
            return RedirectToAction(nameof(Index), new { productId });
        }

        public async Task<IActionResult> DeleteValue(int id)
        {
            var value = await _dataContext.ProductOptionValues.FindAsync(id);
            if (value == null)
            {
                return NotFound();
            }

            var group = await _dataContext.ProductOptionGroups.FindAsync(value.ProductOptionGroupId);
            int productId = group.ProductId;

            _dataContext.ProductOptionValues.Remove(value);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Xóa giá trị tùy chọn thành công!";
            return RedirectToAction(nameof(Index), new { productId });
        }
    }
}
