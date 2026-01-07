using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly DataContext _dataContext;
        public OrderController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }
        public async Task<IActionResult> Index()
        {
            return View(await _dataContext.Orders.OrderByDescending(p => p.OrderId).ToListAsync());
        }
        public async Task<IActionResult> ViewOrder(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode)) return NotFound();

            var order = await _dataContext.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order == null) return NotFound();

            ViewBag.OrderCode = orderCode;
            ViewBag.OrderStatus = order.Status;   // ✅ QUAN TRỌNG

            var detailOrder = await _dataContext.OrderDetails
                .Include(od => od.Product)
                .Where(od => od.OrderCode == orderCode)
                .ToListAsync();

            return View(detailOrder);
        }



        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateOrder(string orderCode, int status)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
                return BadRequest(new { success = false, message = "orderCode is required" });

            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order == null)
                return NotFound(new { success = false, message = "Order not found" });

            order.Status = status;
            await _dataContext.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
