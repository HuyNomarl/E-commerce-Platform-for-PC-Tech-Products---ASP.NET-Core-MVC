using Eshop.Helpers;
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

        public async Task<IActionResult> Index(string slug)
        {
            var category = await _dataContext.Categories
                .Include(x => x.Children)
                .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == 1);

            if (category == null)
            {
                return NotFound();
            }

            var categoryIds = new List<int> { category.Id };

            if (category.Children != null && category.Children.Any())
            {
                categoryIds.AddRange(category.Children
                    .Where(x => x.Status == 1)
                    .Select(x => x.Id));
            }

            var products = await _dataContext.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Include(p => p.ProductImages)
                .Where(p => categoryIds.Contains(p.CategoryId))
                .WhereVisibleOnStorefront(_dataContext)
                .ToListAsync();

            ViewBag.CategoryName = category.Name;

            return View(products);
        }
    }
}
