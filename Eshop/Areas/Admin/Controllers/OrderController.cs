using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
            return View(await _dataContext.Orders
                .OrderByDescending(p => p.OrderId)
                .ToListAsync());
        }

        public async Task<IActionResult> ViewOrder(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode)) return NotFound();

            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order == null) return NotFound();

            var details = await _dataContext.OrderDetails
                .Include(od => od.Product)
                .Where(od => od.OrderCode == orderCode)
                .ToListAsync();

            var vm = new OrderDetailViewModel
            {
                OrderCode = order.OrderCode,
                UserName = order.UserName,
                Status = order.Status,
                OrderDetails = details,
                StatusList = new List<SelectListItem>
        {
            new("Pending","1"),
            new("Processing","2"),
            new("Shipped","3"),
            new("Delivered","4"),
            new("Completed","5"),
            new("Cancelled","6"),
        }
            };

            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrder([FromForm] string orderCode, [FromForm] int status)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
                return BadRequest(new { success = false, message = "orderCode is required" });

            if (!Enum.IsDefined(typeof(OrderStatus), status))
                return BadRequest(new { success = false, message = "Invalid status" });

            using var tx = await _dataContext.Database.BeginTransactionAsync();
            try
            {
                var order = await _dataContext.Orders.FirstOrDefaultAsync(o => o.OrderCode == orderCode);
                if (order == null)
                    return NotFound(new { success = false, message = "Order not found" });

                var oldStatus = (OrderStatus)order.Status;
                var newStatus = (OrderStatus)status;

                if (oldStatus == newStatus)
                    return Json(new { success = true, message = "No changes" });

                // Không cho "mở lại" đơn Cancelled (tuỳ bạn)
                if (oldStatus == OrderStatus.Cancelled && newStatus != OrderStatus.Cancelled)
                    return BadRequest(new { success = false, message = "Order is cancelled. Not allowed to reopen." });

                // Load details & products
                var details = await _dataContext.OrderDetails
                    .Where(d => d.OrderCode == orderCode)
                    .ToListAsync();

                var pids = details.Select(d => d.ProductId).Distinct().ToList();
                var products = await _dataContext.Products
                    .Where(p => pids.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                // ====== SOLD LOGIC ======
                // Khi chuyển sang Completed: +Sold (chỉ 1 lần)
                if (newStatus == OrderStatus.Completed && oldStatus != OrderStatus.Completed)
                {
                    foreach (var d in details)
                        products[d.ProductId].Sold += d.Quantity;
                }

                // Khi rời khỏi Completed (vd Completed -> Cancelled): -Sold
                if (oldStatus == OrderStatus.Completed && newStatus != OrderStatus.Completed)
                {
                    foreach (var d in details)
                    {
                        var p = products[d.ProductId];
                        p.Sold = Math.Max(0, p.Sold - d.Quantity);
                    }
                }

                // ====== STOCK LOGIC ======
                // Khi chuyển sang Cancelled: hoàn kho (Quantity) (chỉ 1 lần)
                if (newStatus == OrderStatus.Cancelled && oldStatus != OrderStatus.Cancelled)
                {
                    foreach (var d in details)
                        products[d.ProductId].Quantity += d.Quantity;
                }

                order.Status = (int)newStatus;

                await _dataContext.SaveChangesAsync();
                await tx.CommitAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
