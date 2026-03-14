using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.ViewComponents
{
    public class CategoryMenuViewComponent : ViewComponent
    {
        private readonly DataContext _context;

        public CategoryMenuViewComponent(DataContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var parentCategories = await _context.Categories
                .Where(x => x.Status == 1 && x.ParentCategoryId == null)
                .Include(x => x.Children.Where(c => c.Status == 1))
                .OrderBy(x => x.Id)
                .ToListAsync();

            return View(parentCategories);
        }
    }
}