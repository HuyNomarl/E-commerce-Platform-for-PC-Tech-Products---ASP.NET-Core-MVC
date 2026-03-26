using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = Eshop.Constants.PolicyNames.AdminOnly)]
    public class CouponController : Controller
    {
        private readonly DataContext _dataContext;

        public CouponController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        // Hàm hỗ trợ nạp danh sách Coupon vào ViewBag
        private async Task LoadCoupons()
        {
            ViewBag.Coupons = await _dataContext.Coupons.OrderByDescending(c => c.Id).ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await LoadCoupons();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CouponModel coupon)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _dataContext.Add(coupon);
                    await _dataContext.SaveChangesAsync();
                    TempData["success"] = "Thêm mã khuyến mãi thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi lưu dữ liệu: " + ex.Message);
                }
            }

            await LoadCoupons();
            TempData["error"] = "Có lỗi xảy ra, vui lòng kiểm tra lại dữ liệu!";

            return View("Index", coupon);
        }
    }
}
