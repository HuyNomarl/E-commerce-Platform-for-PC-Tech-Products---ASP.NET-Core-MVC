using Eshop.Constants;
using Eshop.Models;
using Eshop.Repository;
using Eshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PolicyNames.WarehouseManagement)]
    public class WarehouseController : Controller
    {
        private readonly DataContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly IMemoryCache _memoryCache;

        public WarehouseController(
            DataContext context,
            IInventoryService inventoryService,
            IMemoryCache memoryCache)
        {
            _context = context;
            _inventoryService = inventoryService;
            _memoryCache = memoryCache;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _context.Warehouses
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .ToListAsync();

            return View(items);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new WarehouseModel { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WarehouseModel model)
        {
            if (!ModelState.IsValid) return View(model);

            model.Code = model.Code.Trim().ToUpper();

            if (await _context.Warehouses.AnyAsync(x => x.Code == model.Code))
            {
                ModelState.AddModelError("Code", "Mã kho đã tồn tại.");
                return View(model);
            }

            var hasDefault = await _context.Warehouses.AnyAsync(x => x.IsDefault && x.IsActive);
            if (!hasDefault)
                model.IsDefault = true;

            if (model.IsDefault && !model.IsActive)
            {
                ModelState.AddModelError("IsActive", "Kho mặc định phải luôn ở trạng thái hoạt động.");
                return View(model);
            }

            if (model.IsDefault)
            {
                var defaults = await _context.Warehouses.Where(x => x.IsDefault).ToListAsync();
                defaults.ForEach(x => x.IsDefault = false);
            }

            _context.Warehouses.Add(model);
            await _context.SaveChangesAsync();

            TempData["success"] = "Tạo kho thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.Warehouses.FindAsync(id);
            if (item == null) return NotFound();

            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WarehouseModel model)
        {
            if (id != model.Id) return NotFound();
            if (!ModelState.IsValid) return View(model);

            var item = await _context.Warehouses.FindAsync(id);
            if (item == null) return NotFound();

            model.Code = model.Code.Trim().ToUpper();

            if (await _context.Warehouses.AnyAsync(x => x.Code == model.Code && x.Id != id))
            {
                ModelState.AddModelError("Code", "Mã kho đã tồn tại.");
                return View(model);
            }

            if (model.IsDefault && !model.IsActive)
            {
                ModelState.AddModelError("IsActive", "Kho mặc định phải luôn ở trạng thái hoạt động.");
                return View(model);
            }

            if (!model.IsDefault && item.IsDefault)
            {
                var anotherDefault = await _context.Warehouses
                    .AnyAsync(x => x.Id != id && x.IsDefault && x.IsActive);

                if (!anotherDefault)
                {
                    ModelState.AddModelError("IsDefault", "Hệ thống phải luôn có ít nhất 1 kho mặc định đang hoạt động.");
                    return View(model);
                }
            }

            if (model.IsDefault)
            {
                var defaults = await _context.Warehouses.Where(x => x.IsDefault && x.Id != id).ToListAsync();
                defaults.ForEach(x => x.IsDefault = false);
            }

            var isActiveChanged = item.IsActive != model.IsActive;

            item.Code = model.Code;
            item.Name = model.Name;
            item.Address = model.Address;
            item.IsDefault = model.IsDefault;
            item.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            if (isActiveChanged)
            {
                await _inventoryService.SyncWarehouseProductsAsync(id);
                _memoryCache.Remove(CacheKeys.HomeProducts);
            }

            TempData["success"] = "Cập nhật kho thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}
