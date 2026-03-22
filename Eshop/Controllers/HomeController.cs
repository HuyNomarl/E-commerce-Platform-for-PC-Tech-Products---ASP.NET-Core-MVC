using Eshop.Constants;
using Eshop.Models;
using Eshop.Repository;
using Eshop.Services;
using Eshop.Views.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace Eshop.Controllers
{
    public class HomeController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<AppUserModel> _userManager;
        private readonly IMemoryCache _memoryCache;
        private readonly RecommendationPredictService _recommendService;

        public HomeController(
              ILogger<HomeController> logger,
              DataContext dataContext,
              UserManager<AppUserModel> userManager,
              IMemoryCache memoryCache,
              RecommendationPredictService recommendService)
        {
            _logger = logger;
            _dataContext = dataContext;
            _userManager = userManager;
            _memoryCache = memoryCache;
            _recommendService = recommendService;
        }

        public async Task<IActionResult> Index()
        {
            if (!_memoryCache.TryGetValue(CacheKeys.HomeProducts, out List<ProductModel>? products))
            {
                _logger.LogInformation("CACHE MISS: HomeProducts");

                products = await _dataContext.Products
                    .AsNoTracking()
                    .Include(p => p.Publisher)
                    .Include(p => p.Category)
                    .ToListAsync();

                var productCacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));

                _memoryCache.Set(CacheKeys.HomeProducts, products, productCacheOptions);
            }
            else
            {
                _logger.LogInformation("CACHE HIT: HomeProducts");
            }

            if (!_memoryCache.TryGetValue(CacheKeys.HomeSliders, out List<SliderModel>? sliders))
            {
                _logger.LogInformation("CACHE MISS: HomeSliders");

                sliders = await _dataContext.Sliders
                    .AsNoTracking()
                    .Where(p => p.Status == 1)
                    .ToListAsync();

                var sliderCacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                _memoryCache.Set(CacheKeys.HomeSliders, sliders, sliderCacheOptions);
            }
            else
            {
                _logger.LogInformation("CACHE HIT: HomeSliders");
            }

            List<ProductModel> recommendedProducts = new();

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                recommendedProducts = await _recommendService.RecommendAsync(user.Id, 8);
            }

            ViewBag.Sliders = sliders;
            ViewBag.RecommendedProducts = recommendedProducts;

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

            var existed = await _dataContext.Wishlists
                .AnyAsync(x => x.UserId == user.Id && x.ProductId == productId);

            if (existed)
            {
                return Ok(new { success = true, message = "Đã có trong yêu thích!" });
            }

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
            if (user == null) return Unauthorized();

            var existed = await _dataContext.Compares
                .AnyAsync(x => x.UserId == user.Id && x.ProductId == productId);

            if (existed)
            {
                return Ok(new { success = true, message = "Đã có trong so sánh!" });
            }

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
            if (user == null) return RedirectToAction("Login", "Account");

            var data = await (from c in _dataContext.Compares.AsNoTracking()
                              join p in _dataContext.Products.AsNoTracking()
                                  on c.ProductId equals p.Id
                              where c.UserId == user.Id
                              select new CompareItemVM
                              {
                                  Compare = c,
                                  Product = p
                              }).ToListAsync();

            return View(data);
        }

        public async Task<IActionResult> DeleteCompare(int id)
        {
            var compare = await _dataContext.Compares.FindAsync(id);
            if (compare == null) return NotFound();

            _dataContext.Compares.Remove(compare);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Xóa sản phẩm khỏi so sánh thành công!";
            return RedirectToAction("Compare", "Home");
        }

        public async Task<IActionResult> DeleteWishlist(int id)
        {
            var wishlist = await _dataContext.Wishlists.FindAsync(id);
            if (wishlist == null) return NotFound();

            _dataContext.Wishlists.Remove(wishlist);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Xóa sản phẩm khỏi yêu thích thành công!";
            return RedirectToAction("Wishlist", "Home");
        }

        public async Task<IActionResult> Wishlist()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var data = await (from w in _dataContext.Wishlists.AsNoTracking()
                              join p in _dataContext.Products.AsNoTracking()
                                  on w.ProductId equals p.Id
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
            if (statuscode == 0)
            {
                return View("NotFound");
            }

            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        public async Task<IActionResult> Contact()
        {
            if (!_memoryCache.TryGetValue(CacheKeys.ContactInfo, out ContactModel? contact))
            {
                _logger.LogInformation("CACHE MISS: ContactInfo");

                contact = await _dataContext.Contact
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                var contactCacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(10));

                _memoryCache.Set(CacheKeys.ContactInfo, contact, contactCacheOptions);
            }
            else
            {
                _logger.LogInformation("CACHE HIT: ContactInfo");
            }

            return View(contact);
        }
    }
}