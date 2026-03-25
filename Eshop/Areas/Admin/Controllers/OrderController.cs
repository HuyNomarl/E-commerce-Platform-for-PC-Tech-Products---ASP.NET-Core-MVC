using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Eshop.Services;
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
        private readonly IInventoryService _inventoryService;
        private readonly IOrderStateService _orderStateService;

        public OrderController(
                  DataContext dataContext,
                  IInventoryService inventoryService,
                  IOrderStateService orderStateService)
        {
            _dataContext = dataContext;
            _inventoryService = inventoryService;
            _orderStateService = orderStateService;
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

                var stateCheck = _orderStateService.ValidateTransition(oldStatus, newStatus);

                if (!stateCheck.IsValid)
                    return BadRequest(new { success = false, message = stateCheck.Message });

                if (newStatus == OrderStatus.Cancelled && oldStatus != OrderStatus.Cancelled)
                {
                    await _inventoryService.ReturnOrderAsync(orderCode, User.Identity?.Name, "Admin hủy đơn.");
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