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
            var detailOrder = await _dataContext.OrderDetails.Include(od => od.Product).Where(od => od.OrderCode == orderCode)
            .ToListAsync();

            return View(detailOrder);
        }
    }
}
