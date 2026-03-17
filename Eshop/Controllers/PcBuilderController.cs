using Microsoft.AspNetCore.Mvc;

namespace Eshop.Controllers
{
    public class PcBuilderController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
