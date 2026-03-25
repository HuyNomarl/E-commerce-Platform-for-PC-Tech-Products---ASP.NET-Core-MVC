using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Eshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class InventoryController : Controller
    {
        private readonly DataContext _context;
        private readonly IInventoryService _inventoryService;

        public InventoryController(DataContext context, IInventoryService inventoryService)
        {
            _context = context;
            _inventoryService = inventoryService;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .OrderByDescending(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Image,
                    x.Sold,
                    CachedQuantity = x.Quantity,
                    TotalOnHand = _context.InventoryStocks.Where(s => s.ProductId == x.Id).Sum(s => (int?)s.OnHandQuantity) ?? 0,
                    TotalReserved = _context.InventoryStocks.Where(s => s.ProductId == x.Id).Sum(s => (int?)s.ReservedQuantity) ?? 0
                })
                .ToListAsync();

            return View(products);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BootstrapLegacyStock()
        {
            try
            {
                await _inventoryService.BootstrapLegacyStockAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));
                TempData["success"] = "Đã khởi tạo tồn kho từ dữ liệu cũ.";
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Receive()
        {
            var vm = new AdminInventoryReceiveViewModel();
            await LoadCommonData(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(AdminInventoryReceiveViewModel vm)
        {
            await LoadCommonData(vm);

            if (!ModelState.IsValid)
                return View(vm);

            try
            {
                await _inventoryService.ReceiveAsync(vm, User.FindFirstValue(ClaimTypes.NameIdentifier));
                TempData["success"] = "Nhập kho thành công.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Adjust()
        {
            var vm = new AdminInventoryAdjustViewModel();
            await LoadCommonData(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(AdminInventoryAdjustViewModel vm)
        {
            await LoadCommonData(vm);

            if (!ModelState.IsValid)
                return View(vm);

            try
            {
                await _inventoryService.AdjustAsync(vm, User.FindFirstValue(ClaimTypes.NameIdentifier));
                TempData["success"] = "Điều chỉnh kho thành công.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Transfer()
        {
            var vm = new AdminInventoryTransferViewModel();
            await LoadCommonData(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transfer(AdminInventoryTransferViewModel vm)
        {
            await LoadCommonData(vm);

            if (!ModelState.IsValid)
                return View(vm);

            try
            {
                await _inventoryService.TransferAsync(vm, User.FindFirstValue(ClaimTypes.NameIdentifier));
                TempData["success"] = "Chuyển kho thành công.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(vm);
            }
        }

        public async Task<IActionResult> History(string? referenceCode)
        {
            var query = _context.InventoryTransactions
                .Include(x => x.Warehouse)
                .Include(x => x.Details)
                    .ThenInclude(d => d.Product)
                .OrderByDescending(x => x.CreatedAt)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(referenceCode))
            {
                query = query.Where(x => x.ReferenceCode != null && x.ReferenceCode.Contains(referenceCode));
            }

            var items = await query.Take(100).ToListAsync();
            ViewBag.ReferenceCode = referenceCode;

            return View(items);
        }

        public async Task<IActionResult> ProductStock(int productId)
        {
            var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == productId);
            if (product == null) return NotFound();

            ViewBag.Product = product;

            ViewBag.Stocks = await _context.InventoryStocks
                .Include(x => x.Warehouse)
                .Where(x => x.ProductId == productId)
                .OrderByDescending(x => x.Warehouse.IsDefault)
                .ThenBy(x => x.Warehouse.Name)
                .ToListAsync();

            ViewBag.History = await _context.InventoryTransactionDetails
                .Include(x => x.InventoryTransaction)
                .Include(x => x.Product)
                .Where(x => x.ProductId == productId)
                .OrderByDescending(x => x.Id)
                .Take(50)
                .ToListAsync();

            return View();
        }

        private async Task LoadCommonData(AdminInventoryReceiveViewModel vm)
        {
            vm.Warehouses = await _context.Warehouses
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToListAsync();

            vm.Products = await _context.Products
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToListAsync();
        }

        private async Task LoadCommonData(AdminInventoryAdjustViewModel vm)
        {
            vm.Warehouses = await _context.Warehouses
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToListAsync();

            vm.Products = await _context.Products
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToListAsync();
        }

        private async Task LoadCommonData(AdminInventoryTransferViewModel vm)
        {
            vm.Warehouses = await _context.Warehouses
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToListAsync();

            vm.Products = await _context.Products
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
                .ToListAsync();
        }

        public async Task<IActionResult> Reservations()
        {
            var items = await _context.InventoryReservations
                .Include(x => x.Details)
                    .ThenInclude(d => d.Product)
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .ToListAsync();

            return View(items);
        }

    }
}
