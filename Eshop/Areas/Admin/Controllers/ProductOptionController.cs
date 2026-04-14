using Eshop.Models;
using Eshop.Repository;
using Eshop.Helpers;
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

            if (!CanAccessOptionManagement(product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới có khu vực quản lý option nâng cấp.");
            }

            ViewBag.CanCreateOptions = CanCreateOptions(product);
            ViewBag.IsLegacyOptionCleanupMode = ProductCatalogAdminRules.HasLegacyOptions(product);
            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> CreateGroup(int productId)
        {
            var product = await _dataContext.Products
                .Include(p => p.OptionGroups)
                .FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
            {
                return NotFound();
            }

            if (!CanCreateOptions(product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới được thêm option nâng cấp.");
            }

            ViewBag.Product = product;
            return View(new ProductOptionGroupModel { ProductId = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup(ProductOptionGroupModel model)
        {
            var product = await _dataContext.Products
                .Include(p => p.OptionGroups)
                .FirstOrDefaultAsync(p => p.Id == model.ProductId);
            if (product == null)
            {
                return NotFound();
            }

            if (!CanCreateOptions(product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới được thêm option nâng cấp.");
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

            if (!CanCreateOptions(group.Product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới được thêm option nâng cấp.");
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

            if (!CanCreateOptions(group.Product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới được thêm option nâng cấp.");
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
            var group = await _dataContext.ProductOptionGroups
                .Include(g => g.Product)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (group == null)
            {
                return NotFound();
            }

            if (!CanAccessOptionManagement(group.Product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới có khu vực quản lý option nâng cấp.");
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

            var group = await _dataContext.ProductOptionGroups
                .Include(g => g.Product)
                .FirstOrDefaultAsync(g => g.Id == id);
            if (group == null)
            {
                return NotFound();
            }

            if (!CanAccessOptionManagement(group.Product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới có khu vực quản lý option nâng cấp.");
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
            var value = await _dataContext.ProductOptionValues
                .Include(x => x.ProductOptionGroup)
                    .ThenInclude(g => g.Product)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (value == null)
            {
                return NotFound();
            }

            if (!CanAccessOptionManagement(value.ProductOptionGroup.Product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới có khu vực quản lý option nâng cấp.");
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

            var value = await _dataContext.ProductOptionValues
                .Include(x => x.ProductOptionGroup)
                    .ThenInclude(g => g.Product)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (value == null)
            {
                return NotFound();
            }

            if (!CanAccessOptionManagement(value.ProductOptionGroup.Product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới có khu vực quản lý option nâng cấp.");
            }

            value.Value = model.Value;
            value.AdditionalPrice = model.AdditionalPrice;
            value.Stock = model.Stock;
            value.Status = model.Status;
            value.IsDefault = model.IsDefault;
            value.SortOrder = model.SortOrder;

            await _dataContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { productId = value.ProductOptionGroup.ProductId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            var group = await _dataContext.ProductOptionGroups
                .Include(g => g.OptionValues)
                .Include(g => g.Product)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null)
            {
                return NotFound();
            }

            if (!CanAccessOptionManagement(group.Product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới có khu vực quản lý option nâng cấp.");
            }

            int productId = group.ProductId;

            _dataContext.ProductOptionValues.RemoveRange(group.OptionValues);
            _dataContext.ProductOptionGroups.Remove(group);

            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Xóa nhóm tùy chọn thành công!";
            return RedirectToAction(nameof(Index), new { productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteValue(int id)
        {
            var value = await _dataContext.ProductOptionValues
                .Include(x => x.ProductOptionGroup)
                    .ThenInclude(g => g.Product)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (value == null)
            {
                return NotFound();
            }

            if (!CanAccessOptionManagement(value.ProductOptionGroup.Product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới có khu vực quản lý option nâng cấp.");
            }

            var group = value.ProductOptionGroup;
            if (group == null)
            {
                return NotFound();
            }

            if (!CanAccessOptionManagement(group.Product))
            {
                return RedirectToProductIndexWithError("Chỉ PC dựng sẵn mới có khu vực quản lý option nâng cấp.");
            }

            int productId = group.ProductId;

            _dataContext.ProductOptionValues.Remove(value);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Xóa giá trị tùy chọn thành công!";
            return RedirectToAction(nameof(Index), new { productId });
        }

        private static bool CanCreateOptions(ProductModel? product)
        {
            return ProductCatalogAdminRules.CanConfigureOptions(product);
        }

        private static bool CanAccessOptionManagement(ProductModel? product)
        {
            return ProductCatalogAdminRules.CanAccessOptionManagement(product);
        }

        private IActionResult RedirectToProductIndexWithError(string message)
        {
            TempData["error"] = message;
            return RedirectToAction("Index", "Product", new { area = "Admin" });
        }
    }
}
