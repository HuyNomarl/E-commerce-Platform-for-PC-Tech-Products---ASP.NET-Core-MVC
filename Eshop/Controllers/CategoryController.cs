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

        public async Task<IActionResult> Index(String Slug = "")
        {
            CategoryModel category =  _dataContext.Categories.Where(p => p.Slug == Slug && p.Status == 1).FirstOrDefault();
            if (category == null)
            {
                return RedirectToAction("Index");
            }

            var productsByCategory = _dataContext.Products.Where(p => p.CategoryId == category.Id );

            return View(await productsByCategory.OrderByDescending(p => p.Id).ToListAsync());
        }

    }
}
