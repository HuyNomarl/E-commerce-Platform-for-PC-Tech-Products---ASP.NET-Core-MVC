using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class ProductController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(IWebHostEnvironment webHostEnvironment, DataContext dataContext)
        {
            _dataContext = dataContext;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _dataContext.Products
                .OrderByDescending(p => p.Id)
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .ToListAsync();

            return View(products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            LoadViewBags();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductModel product)
        {
            LoadViewBags(product.CategoryId, product.PublisherId);

            if (!ModelState.IsValid)
            {
                TempData["error"] = "Vui lòng kiểm tra lại thông tin.";
                return View(product);
            }

            product.Slug = GenerateSlug(product.Name);

            var slugExists = await _dataContext.Products
                .AnyAsync(p => p.Slug == product.Slug);

            if (slugExists)
            {
                ModelState.AddModelError("", "Sản phẩm đã tồn tại.");
                return View(product);
            }

            if (product.ImageUpload != null && product.ImageUpload.Length > 0)
            {
                product.Image = await SaveProductImage(product.ImageUpload);
            }
            else
            {
                product.Image = "noname.jpg";
            }

            _dataContext.Products.Add(product);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Thêm sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _dataContext.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            LoadViewBags(product.CategoryId, product.PublisherId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductModel product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            LoadViewBags(product.CategoryId, product.PublisherId);

            if (!ModelState.IsValid)
            {
                TempData["error"] = "Vui lòng kiểm tra lại thông tin.";
                return View(product);
            }

            var existingProduct = await _dataContext.Products.FindAsync(id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            string newSlug = GenerateSlug(product.Name);

            var slugExists = await _dataContext.Products
                .AnyAsync(p => p.Slug == newSlug && p.Id != product.Id);

            if (slugExists)
            {
                ModelState.AddModelError("", "Sản phẩm với tên này đã tồn tại!");
                return View(product);
            }

            if (product.ImageUpload != null && product.ImageUpload.Length > 0)
            {
                if (!string.IsNullOrEmpty(existingProduct.Image) && existingProduct.Image != "noname.jpg")
                {
                    DeleteProductImage(existingProduct.Image);
                }

                existingProduct.Image = await SaveProductImage(product.ImageUpload);
            }

            existingProduct.Name = product.Name;
            existingProduct.Slug = newSlug;
            existingProduct.Description = product.Description;
            existingProduct.Price = product.Price;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.PublisherId = product.PublisherId;
            existingProduct.IsPcBuild = product.IsPcBuild;

            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Cập nhật sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var product = await _dataContext.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(product.Image) && product.Image != "noname.jpg")
            {
                DeleteProductImage(product.Image);
            }

            _dataContext.Products.Remove(product);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Xóa sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> AddQuantity(int id)
        {
            var product = await _dataContext.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var logs = await _dataContext.productQuantityModels
                .Where(pq => pq.ProductId == id)
                .OrderByDescending(pq => pq.DateCreate)
                .ToListAsync();

            ViewBag.ProductQuantityLogs = logs;
            ViewBag.Id = id;

            return View(new ProductQuantityModel
            {
                ProductId = id,
                Quantity = 1
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StoreProductQuantity(ProductQuantityModel productQuantityModel, int id)
        {
            var product = await _dataContext.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            product.Quantity += productQuantityModel.Quantity;

            var log = new ProductQuantityModel
            {
                ProductId = id,
                Quantity = productQuantityModel.Quantity,
                DateCreate = DateTime.Now
            };

            _dataContext.productQuantityModels.Add(log);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Cập nhật số lượng thành công!";
            return RedirectToAction(nameof(AddQuantity), new { id });
        }

        private void LoadViewBags(object? selectedCategory = null, object? selectedPublisher = null)
        {
            ViewBag.Categories = new SelectList(_dataContext.Categories.ToList(), "Id", "Name", selectedCategory);
            ViewBag.Publishers = new SelectList(_dataContext.Publishers.ToList(), "Id", "Name", selectedPublisher);
        }

        private string GenerateSlug(string name)
        {
            return name.Trim().ToLower().Replace(" ", "-");
        }

        private async Task<string> SaveProductImage(IFormFile imageUpload)
        {
            string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");

            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            string imageName = Guid.NewGuid() + "_" + imageUpload.FileName;
            string filePath = Path.Combine(uploadDir, imageName);

            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageUpload.CopyToAsync(fileStream);
            }

            return imageName;
        }

        private void DeleteProductImage(string imageName)
        {
            string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
            string filePath = Path.Combine(uploadDir, imageName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}