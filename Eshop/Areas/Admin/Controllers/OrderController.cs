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
            var orders = await _dataContext.Orders
                .OrderByDescending(p => p.OrderId)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> ViewOrder(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
                return NotFound();

            var order = await _dataContext.Orders
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

            if (order == null)
                return NotFound();

            var details = await _dataContext.OrderDetails
                .Include(od => od.Product)
                .Where(od => od.OrderId == order.OrderId)
                .ToListAsync();

            var vm = new OrderDetailViewModel
            {
                OrderCode = order.OrderCode,
                UserName = order.UserName,
                FullName = order.FullName,
                Phone = order.Phone,
                Email = order.Email,
                Address = order.Address,
                Province = order.Province,
                District = order.District,
                Ward = order.Ward,
                Note = order.Note,
                Status = order.Status,
                OrderDetails = details,
                StatusList = new List<SelectListItem>
                {
                    new("Pending", "1"),
                    new("Processing", "2"),
                    new("Shipped", "3"),
                    new("Delivered", "4"),
                    new("Completed", "5"),
                    new("Cancelled", "6"),
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

            await using var tx = await _dataContext.Database.BeginTransactionAsync();

            try
            {
                var order = await _dataContext.Orders
                    .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

                if (order == null)
                    return NotFound(new { success = false, message = "Order not found" });

                var oldStatus = (OrderStatus)order.Status;
                var newStatus = (OrderStatus)status;

                if (oldStatus == newStatus)
                    return Json(new { success = true, message = "No changes" });

                // Không cho mở lại đơn đã hủy
                if (oldStatus == OrderStatus.Cancelled && newStatus != OrderStatus.Cancelled)
                    return BadRequest(new { success = false, message = "Order is cancelled. Not allowed to reopen." });

                //Hủy đơn hàng: cộng lại số lượng vào kho
                if (newStatus == OrderStatus.Cancelled && oldStatus != OrderStatus.Cancelled)
                {
                    var details = await _dataContext.OrderDetails
                        .Where(d => d.OrderId == order.OrderId)
                        .ToListAsync();

                    var productIds = details.Select(d => d.ProductId).Distinct().ToList();

                    var products = await _dataContext.Products
                        .Where(p => productIds.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id);

                    foreach (var detail in details)
                    {
                        if (products.TryGetValue(detail.ProductId, out var product))
                        {
                            product.Quantity += detail.Quantity;
                            product.Sold = Math.Max(0, product.Sold - detail.Quantity);
                        }
                    }
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