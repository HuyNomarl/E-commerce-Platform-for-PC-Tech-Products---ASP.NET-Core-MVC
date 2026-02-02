using Eshop.Models;
using Eshop.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

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
            return View(await _dataContext.Products.OrderByDescending(p => p.Id).Include(p => p.Category).Include(p => p.Category).Include(p => p.Publisher).ToListAsync());
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Categories = new SelectList(_dataContext.Categories.ToList(), "Id", "Name");
            ViewBag.Publishers = new SelectList(_dataContext.Publishers.ToList(), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductModel product)
        {
            ViewBag.Categories = new SelectList(_dataContext.Categories.ToList(), "Id", "Name", product.CategoryId);
            ViewBag.Publishers = new SelectList(_dataContext.Publishers.ToList(), "Id", "Name", product.PublisherId);

            if (ModelState.IsValid)
            {
                product.Slug = product.Name.ToLower().Replace(" ", "-");
                var Slug = await _dataContext.Products.FirstOrDefaultAsync(p => p.Slug == product.Slug);
                if (Slug != null)
                {
                    ModelState.AddModelError("", "Sản phẩm đã tồn tại!");
                    return View(product);
                }

                if (product.ImageUpload != null)
                {
                    string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
                    string imageName = Guid.NewGuid().ToString() + "_" + product.ImageUpload.FileName;
                    string filePath = Path.Combine(uploadDir, imageName);

                    FileStream fileStream = new FileStream(filePath, FileMode.Create);
                    await product.ImageUpload.CopyToAsync(fileStream);
                    fileStream.Close();
                    product.Image = imageName;
                }

                _dataContext.Add(product);
                await _dataContext.SaveChangesAsync();
                TempData["success"] = "Thêm sản phẩm thành công!";
                return RedirectToAction("Index");
                //await _dataContext.SaveChangesAsync();
            }
            else
            {
                TempData["error"] = "Model đang lỗi, xin thử lại sau!";
                List<string> errors = new List<string>();
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        errors.Add(error.ErrorMessage);
                    }
                }
                string errorMessages = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return BadRequest(errorMessages);
            }
            return View(product);
        }

        public async Task<IActionResult> Edit(int Id)
        {
            ProductModel product = await _dataContext.Products.FindAsync(Id);

            ViewBag.Categories = new SelectList(_dataContext.Categories.ToList(), "Id", "Name", product.CategoryId);
            ViewBag.Publishers = new SelectList(_dataContext.Publishers.ToList(), "Id", "Name", product.PublisherId);

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int Id, ProductModel product, IFormFile? ImageUpload)
        {
            // Đảm bảo Id từ route khớp với Id của product
            if (Id != product.Id)
            {
                return NotFound();
            }

            ViewBag.Categories = new SelectList(_dataContext.Categories.ToList(), "Id", "Name", product.CategoryId);
            ViewBag.Publishers = new SelectList(_dataContext.Publishers.ToList(), "Id", "Name", product.PublisherId);

            if (ModelState.IsValid)
            {
                // Tạo slug mới
                string newSlug = product.Name.ToLower().Replace(" ", "-");

                // Kiểm tra slug trùng, nhưng bỏ qua chính sản phẩm đang edit
                var slugExists = await _dataContext.Products
                    .AnyAsync(p => p.Slug == newSlug && p.Id != product.Id);

                if (slugExists)
                {
                    ModelState.AddModelError("", "Sản phẩm với tên này đã tồn tại!");
                    return View(product);
                }

                // Lấy entity hiện tại từ DB để update
                var existingProduct = await _dataContext.Products.FindAsync(Id);
                if (existingProduct == null)
                {
                    return NotFound();
                }

                // Xử lý upload ảnh mới
                if (ImageUpload != null && ImageUpload.Length > 0)
                {
                    // Xóa ảnh cũ nếu không phải ảnh mặc định
                    if (!string.IsNullOrEmpty(existingProduct.Image) &&
                        existingProduct.Image != "noname.jpg")
                    {
                        var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, "media/products", existingProduct.Image);
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    // Lưu ảnh mới
                    string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
                    string imageName = Guid.NewGuid().ToString() + "_" + ImageUpload.FileName;
                    string filePath = Path.Combine(uploadDir, imageName);

                    using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageUpload.CopyToAsync(fileStream);
                    }

                    existingProduct.Image = imageName; // Gán ảnh mới
                }


                // Cập nhật các field
                existingProduct.Name = product.Name;
                existingProduct.Slug = newSlug;
                existingProduct.Description = product.Description;
                existingProduct.Price = product.Price;
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.PublisherId = product.PublisherId;

                await _dataContext.SaveChangesAsync();

                TempData["success"] = "Cập nhật sản phẩm thành công!";
                return RedirectToAction("Index");
            }

            // Nếu ModelState không hợp lệ
            TempData["error"] = "Có lỗi xảy ra, vui lòng kiểm tra lại thông tin!";
            return View(product);
        }

        public async Task<IActionResult> Delete(int Id)
        {
            ProductModel product = await _dataContext.Products.FindAsync(Id);
            if (!string.Equals(product.Image, "noname.jpg"))
            {
                string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
                string filePath = Path.Combine(uploadDir, product.Image);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            _dataContext.Products.Remove(product);
            await _dataContext.SaveChangesAsync();
            TempData["success"] = "Xóa sản phẩm thành công!";
            return RedirectToAction("Index");

        }
        [HttpGet]
        public async Task<IActionResult> AddQuantity(int Id)
        {
            ViewBag.Id = Id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StoreProductQuantity(ProductQuantityModel productQuantityModel, int id)
        {
            var product = await _dataContext.Products.FindAsync(id);
            if (product == null) return NotFound();

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
            return RedirectToAction("AddQuantity", new { Id = id });
        }


    }
}
