using Microsoft.AspNetCore.Mvc;

namespace Eshop.Controllers
{
    public class CategoryController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

    }
}
