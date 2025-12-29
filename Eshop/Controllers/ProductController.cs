using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

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

        //public IActionResult Create()
        //{
        //    ViewBag.Categories = new SelectList(_dataContext.Categories, "Id", "Name");
        //    ViewBag.Publishers = new SelectList(_dataContext.Publishers, "Id", "Name");
        //    return View();
        //}

        //public async Task<IActionResult> Create(ProductModel product)
        //{

        //    ViewBag.Categories = new SelectList(_dataContext.Categories, "Id", "Name", product.CategoryId);
        //    ViewBag.Publishers = new SelectList(_dataContext.Publishers, "Id", "Name", product.PublisherId);

        //    return View(product);
        //}
    }
}
