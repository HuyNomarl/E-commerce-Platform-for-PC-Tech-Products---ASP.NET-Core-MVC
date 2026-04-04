using Eshop.Helpers;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Controllers
{
    public class PublisherController : Controller
    {
        private readonly DataContext _dataContext;

        public PublisherController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<IActionResult> Index(string Slug = "")
        {
            var publisher = await _dataContext.Publishers
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Slug == Slug && p.status == 1);

            if (publisher == null)
            {
                return NotFound();
            }

            var productsByPublisher = _dataContext.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Include(p => p.ProductImages)
                .Where(p => p.PublisherId == publisher.Id)
                .WhereVisibleOnStorefront(_dataContext);

            return View(await productsByPublisher.OrderByDescending(p => p.Id).ToListAsync());
        }
    }
}
