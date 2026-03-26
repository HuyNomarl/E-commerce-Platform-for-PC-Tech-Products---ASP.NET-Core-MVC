using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = Eshop.Constants.PolicyNames.AdminOnly)]
    public class ShippingController : Controller
    {
        private readonly DataContext _dataContext;

        public ShippingController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<IActionResult> Index()
        {
            var shippingList = await _dataContext.Shippings
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            ViewBag.ShippingList = shippingList;
            return View();
        }

        // ====== STORE SHIPPING - V2 (Tỉnh -> Phường) ======
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StoreShippingV2(
            string cityCode,
            string wardCode,
            string cityName,
            string wardName,
            decimal cost)
        {
            cityCode = (cityCode ?? "").Trim();
            wardCode = (wardCode ?? "").Trim();

            cityName = (cityName ?? "").Trim();
            wardName = (wardName ?? "").Trim();

            if (string.IsNullOrWhiteSpace(cityCode) || cityCode == "0" ||
                string.IsNullOrWhiteSpace(wardCode) || wardCode == "0" ||
                string.IsNullOrWhiteSpace(cityName) ||
                string.IsNullOrWhiteSpace(wardName) ||
                cost <= 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập đầy đủ thông tin hợp lệ!" });
            }

            try
            {
                // v2: check trùng theo (CityCode + WardCode)
                var exists = await _dataContext.Shippings.AnyAsync(x =>
                    x.CityCode == cityCode && x.WardCode == wardCode
                );

                if (exists)
                {
                    return Ok(new { duplicate = true, message = "Dữ liệu trùng lặp (địa chỉ này đã có shipping)." });
                }

                var shipping = new ShippingModel
                {
                    CityCode = cityCode,
                    WardCode = wardCode,

                    City = cityName,
                    Ward = wardName,

                    // v2 không có district -> để rỗng
                    DistrictCode = "",
                    District = "",

                    ShippingCost = cost
                };

                _dataContext.Shippings.Add(shipping);
                await _dataContext.SaveChangesAsync();

                return Ok(new { success = true, message = "Thêm Shipping cost thành công" });
            }
            catch
            {
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi lưu dữ liệu." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteShipping(int id)
        {
            var shipping = await _dataContext.Shippings.FindAsync(id);
            if (shipping == null)
            {
                return NotFound(new { success = false, message = "Shipping cost không tồn tại" });
            }

            try
            {
                _dataContext.Shippings.Remove(shipping);
                await _dataContext.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi xoá dữ liệu." });
            }
        }

        // ====== PROXY: GET PROVINCES (V2) ======
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetProvincesV2()
        {
            var url = "https://provinces.open-api.vn/api/v2/";

            using var http = new HttpClient();
            var res = await http.GetAsync(url);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, body);

            return Content(body, "application/json");
        }

        // ====== PROXY: GET WARDS BY PROVINCE (V2) ======
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetWardsByProvinceV2(int provinceCode)
        {
            var url = $"https://provinces.open-api.vn/api/v2/p/{provinceCode}?depth=2";

            using var http = new HttpClient();
            var res = await http.GetAsync(url);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, body);

            return Content(body, "application/json");
        }
    }
}
