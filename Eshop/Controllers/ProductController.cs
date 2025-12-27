using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;

namespace Eshop.Controllers
{
    public class ProductController : Controller
    {
        private readonly DataContext _dataContext;
        public ProductController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }
        public IActionResult Index()
        {
            return View();
        }
        public async Task<IActionResult> Details(int Id)
        {
            if (Id == null)
            {
                return RedirectToAction("Index");
            }
            var productsById = _dataContext.Products.Where(p => p.Id == Id).FirstOrDefault();

            return View(productsById);
        }
    }
}
