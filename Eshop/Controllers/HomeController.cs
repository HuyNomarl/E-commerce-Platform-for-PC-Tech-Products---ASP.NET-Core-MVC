using Eshop.Models;
using Eshop.Repository;
using Eshop.Views.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Eshop.Controllers
{
    public class HomeController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<AppUserModel> _userManager;

        public HomeController(ILogger<HomeController> logger, DataContext dataContext, UserManager<AppUserModel> userManager)
        {
            _logger = logger;
            _dataContext = dataContext;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            var products = _dataContext.Products.Include("Publisher").Include("Category").ToList();
            var sliders = _dataContext.Sliders.Where(p => p.Status == 1).ToList();
            ViewBag.Sliders = sliders;
            return View(products);
        }

        public IActionResult Privacy()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddWishlist(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // chặn trùng
            var existed = await _dataContext.Wishlists
                .AnyAsync(x => x.UserId == user.Id && x.ProductId == productId);
            if (existed) return Ok(new { success = true, message = "Đã có trong yêu thích!" });

            var wishlist = new WishlistModel
            {
                ProductId = productId,
                UserId = user.Id
            };

            _dataContext.Wishlists.Add(wishlist);
            await _dataContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã thêm vào yêu thích!" });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCompare(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            var compare = new CompareModel
            {
                ProductId = productId,
                UserId = user.Id
            };
            _dataContext.Compares.Add(compare);
            try
            {
                await _dataContext.SaveChangesAsync();
                return Ok(new
                {
                    success = true,
                    message = "Đã thêm vào so sánh!"
                });
            }
            catch (Exception)
            {
                return StatusCode(500, "Error while adding");
            }
        }

        public async Task<IActionResult> Compare()
        {
            var user = await _userManager.GetUserAsync(User);

            var data = await (from c in _dataContext.Compares
                              join p in _dataContext.Products on c.ProductId equals p.Id
                              where c.UserId == user.Id
                              select new CompareItemVM
                              {
                                  Compare = c,
                                  Product = p
                              }).ToListAsync();

            return View(data);
        }
        public async Task<IActionResult> DeleteCompare(int Id)
        {
            CompareModel compare = await _dataContext.Compares.FindAsync(Id);

            _dataContext.Compares.Remove(compare);
            await _dataContext.SaveChangesAsync();
            TempData["success"] = "Xóa sản phẩm khỏi so sánh thành công!";
            return RedirectToAction("Compare", "Home");
        }
        public async Task<IActionResult> DeleteWishlist(int Id)
        {
            WishlistModel wishlist = await _dataContext.Wishlists.FindAsync(Id);

            _dataContext.Wishlists.Remove(wishlist);
            await _dataContext.SaveChangesAsync();
            TempData["success"] = "Xóa sản phẩm khỏi yêu thích thành công!";
            return RedirectToAction("Wishlist", "Home");
        }

        public async Task<IActionResult> Wishlist()
        {
            var user = await _userManager.GetUserAsync(User);

            var data = await (from w in _dataContext.Wishlists
                              join p in _dataContext.Products on w.ProductId equals p.Id
                              where w.UserId == user.Id
                              select new WishlistItemVM
                              {
                                  Wishlist = w,
                                  Product = p
                              }).ToListAsync();

            return View(data);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int statuscode)
        {
            if (statuscode == 0 )
            {
                return View("NotFound");
            }
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> Contact()
        {
            var contact = await _dataContext.Contact.FirstOrDefaultAsync();
            return View(contact);
        }
    }
}
