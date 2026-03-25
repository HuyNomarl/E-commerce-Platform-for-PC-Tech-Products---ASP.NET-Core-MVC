using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Details(int id)
        {
            var product = await _dataContext.Products
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Include(p => p.RatingModel)
                .Include(p => p.OptionGroups)
                    .ThenInclude(g => g.OptionValues)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return RedirectToAction(nameof(Index));

            var relatedProducts = await _dataContext.Products
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .Take(4)
                .ToListAsync();

            var totalOnHand = await _dataContext.InventoryStocks
                .Where(x => x.ProductId == id)
                .SumAsync(x => (int?)x.OnHandQuantity) ?? 0;

            var totalReserved = await _dataContext.InventoryStocks
                .Where(x => x.ProductId == id)
                .SumAsync(x => (int?)x.ReservedQuantity) ?? 0;

            ViewBag.RelatedProducts = relatedProducts;

            var viewModel = new ProductDetailViewModel
            {
                ProductDetail = product,
                InventorySummary = new ProductInventorySummaryViewModel
                {
                    ProductId = id,
                    TotalOnHand = totalOnHand,
                    TotalReserved = totalReserved
                }
            };

            return View(viewModel);
        }



        public async Task<IActionResult> Search(string searchTerm)
        {
           var products = await _dataContext.Products
                .Where(p => p.Name.Contains(searchTerm) || p.Description.Contains(searchTerm))
                .ToListAsync();

            ViewBag.Keyword = searchTerm;
            return View(products);
        }

        public async Task<IActionResult> CommentProduct(RatingModel rating)
        {
            if (ModelState.IsValid)
            {
                var ratingEntity = new RatingModel
                {
                    ProductId = rating.ProductId,
                    Name = rating.Name,
                    Email = rating.Email,
                    Comment = rating.Comment,
                    Stars = rating.Stars,
                };

                _dataContext.RatingModels.Add(ratingEntity);
                await _dataContext.SaveChangesAsync();

                TempData["succes"] = "Them danh gia thanh cong!";

                return Redirect(Request.Headers["Referer"]);
            }
            else
            {
                return RedirectToAction("Detail", new { id = rating.ProductId });
            }
            return Redirect(Request.Headers["Referer"]);
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
