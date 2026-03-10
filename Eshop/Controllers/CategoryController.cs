using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Controllers
{
    public class CategoryController : Controller
    {
        private readonly DataContext _dataContext;
        public CategoryController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<IActionResult> Index(
            string Slug = "",
            string sort_by = "",
            decimal? min_price = null,
            decimal? max_price = null
        )
        {
            var category = await _dataContext.Categories
                .FirstOrDefaultAsync(c => c.Slug == Slug && c.Status == 1);

            if (category == null)
                return RedirectToAction("Index", "Home"); // tránh tự gọi lại chính nó

            // Query sản phẩm theo category
            var productsQuery = _dataContext.Products
                .AsNoTracking()
                .Where(p => p.CategoryId == category.Id);

            // Lọc giá
            if (min_price.HasValue)
                productsQuery = productsQuery.Where(p => p.Price >= min_price.Value);

            if (max_price.HasValue)
                productsQuery = productsQuery.Where(p => p.Price <= max_price.Value);

            // Sort
            productsQuery = sort_by switch
            {
                "price_increase" => productsQuery.OrderBy(p => p.Price),
                "price_decrease" => productsQuery.OrderByDescending(p => p.Price),
                "price_newest" => productsQuery.OrderByDescending(p => p.Id),
                "price_oldest" => productsQuery.OrderBy(p => p.Id),
                _ => productsQuery.OrderByDescending(p => p.Id),
            };

            // Nếu view cần item.Category.Name / item.Publisher.Name thì nên Include để khỏi null
            var products = await productsQuery
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .ToListAsync();

            // đẩy lại các giá trị để view giữ trạng thái
            ViewBag.CurrentSlug = Slug;
            ViewBag.SortBy = sort_by;
            ViewBag.MinPrice = min_price;
            ViewBag.MaxPrice = max_price;

            return View(products);
        }
    }
}