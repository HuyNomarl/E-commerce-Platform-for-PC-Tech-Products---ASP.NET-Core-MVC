using Eshop.Models;
using Eshop.Helpers;
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

            var currentStatus = OrderDisplayHelper.ToOrderStatus(order.Status);

            var vm = new OrderDetailViewModel
            {
                OrderCode = order.OrderCode,
                UserName = order.UserName,
                CreatedTime = order.CreatedTime,
                FullName = order.FullName,
                Phone = order.Phone,
                Email = order.Email,
                Address = order.Address,
                Province = order.Province,
                District = order.District,
                Ward = order.Ward,
                Note = order.Note,
                PaymentMethod = order.PaymentMethod,
                SubTotal = order.SubTotal,
                DiscountAmount = order.DiscountAmount,
                ShippingCost = order.ShippingCost,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                OrderDetails = details,
                StatusList = _orderStateService.GetAvailableStatuses(currentStatus)
                    .Select(statusValue => new SelectListItem(
                        OrderDisplayHelper.GetStatusLabel(statusValue),
                        ((int)statusValue).ToString()))
                    .ToList()
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
                    await _inventoryService.RevertOrderInventoryAsync(
                        orderCode,
                        order.ReservationCode,
                        User.Identity?.Name,
                        "Admin hủy đơn.");
                }


                order.Status = (int)newStatus;

                await _dataContext.SaveChangesAsync();
                await tx.CommitAsync();

                return Json(new
                {
                    success = true,
                    statusLabel = OrderDisplayHelper.GetStatusLabel(newStatus),
                    statusBadgeClass = OrderDisplayHelper.GetBootstrapBadgeClass(newStatus)
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
