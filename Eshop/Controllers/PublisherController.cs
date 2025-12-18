using Eshop.Models;
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

        public async Task<IActionResult> Index(String Slug = "")
        {
            PublisherModel publisher = _dataContext.Publishers.Where(p => p.Slug == Slug && p.status == 1).FirstOrDefault();
            if (publisher == null)
            {
                return RedirectToAction("Index");
            }

            var productsByCategory = _dataContext.Products.Where(p => p.PublisherId == publisher.Id);

            return View(await productsByCategory.OrderByDescending(p => p.Id).ToListAsync());
        }
    }
}
