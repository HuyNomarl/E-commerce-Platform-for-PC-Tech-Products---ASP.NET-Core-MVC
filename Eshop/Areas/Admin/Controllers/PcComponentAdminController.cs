using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PcComponentAdminController : Controller
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public PcComponentAdminController(DataContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _context.Products
                .Include(x => x.Publisher)
                .Include(x => x.Category)
                .Where(x => x.ProductType == ProductType.Component || x.ProductType == ProductType.Monitor)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new PcComponentCreateViewModel
            {
                Product = new ProductModel
                {
                    ProductType = ProductType.Component,
                    Quantity = 1,
                    Price = 1
                }
            };

            await LoadDropdowns(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PcComponentCreateViewModel vm)
        {
            await LoadDropdowns(vm);

            if (vm.Product.ComponentType == null || vm.Product.ComponentType == PcComponentType.None)
            {
                ModelState.AddModelError("Product.ComponentType", "Yêu cầu chọn loại linh kiện.");
            }

            vm.Product.ProductType = vm.Product.ComponentType == PcComponentType.Monitor
                ? ProductType.Monitor
                : ProductType.Component;

            vm.Product.IsPcBuild = false;

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            vm.Product.Slug = GenerateSlug(vm.Product.Name);

            if (vm.Product.ImageUpload != null)
            {
                vm.Product.Image = await SaveImageAsync(vm.Product.ImageUpload);
            }

            _context.Products.Add(vm.Product);
            await _context.SaveChangesAsync();

            await SaveOrUpdateSpecifications(vm.Product.Id, vm.Product.ComponentType!.Value, vm.Specifications);

            TempData["success"] = "Tạo linh kiện thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .Include(x => x.Specifications)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            if (product.ProductType != ProductType.Component && product.ProductType != ProductType.Monitor)
            {
                return NotFound();
            }

            var vm = new PcComponentCreateViewModel
            {
                Product = product
            };

            await LoadDropdowns(vm);

            if (product.ComponentType.HasValue)
            {
                var defs = await _context.SpecificationDefinitions
                    .Where(x => x.ComponentType == product.ComponentType)
                    .OrderBy(x => x.SortOrder)
                    .ToListAsync();

                vm.Specifications = defs.Select(def =>
                {
                    var existing = product.Specifications.FirstOrDefault(x => x.SpecificationDefinitionId == def.Id);

                    return new ProductSpecificationInputViewModel
                    {
                        SpecificationDefinitionId = def.Id,
                        Name = def.Name,
                        Code = def.Code,
                        DataType = def.DataType,
                        Unit = def.Unit,
                        ValueText = existing?.ValueText,
                        ValueNumber = existing?.ValueNumber,
                        ValueBool = existing?.ValueBool,
                        ValueJson = existing?.ValueJson
                    };
                }).ToList();
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PcComponentCreateViewModel vm)
        {
            await LoadDropdowns(vm);

            var product = await _context.Products
                .Include(x => x.Specifications)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            if (vm.Product.ComponentType == null || vm.Product.ComponentType == PcComponentType.None)
            {
                ModelState.AddModelError("Product.ComponentType", "Yêu cầu chọn loại linh kiện.");
            }

            vm.Product.ProductType = vm.Product.ComponentType == PcComponentType.Monitor
                ? ProductType.Monitor
                : ProductType.Component;

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            product.Name = vm.Product.Name;
            product.Description = vm.Product.Description;
            product.Price = vm.Product.Price;
            product.Quantity = vm.Product.Quantity;
            product.CategoryId = vm.Product.CategoryId;
            product.PublisherId = vm.Product.PublisherId;
            product.ComponentType = vm.Product.ComponentType;
            product.ProductType = vm.Product.ProductType;
            product.IsPcBuild = false;
            product.Slug = GenerateSlug(vm.Product.Name);

            if (vm.Product.ImageUpload != null)
            {
                if (!string.IsNullOrWhiteSpace(product.Image))
                {
                    DeleteImageIfExists(product.Image);
                }

                product.Image = await SaveImageAsync(vm.Product.ImageUpload);
            }

            await SaveOrUpdateSpecifications(product.Id, product.ComponentType!.Value, vm.Specifications);

            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            TempData["success"] = "Cập nhật linh kiện thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(x => x.Specifications)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (product == null)
            {
                TempData["error"] = "Không tìm thấy linh kiện.";
                return RedirectToAction(nameof(Index));
            }

            var usedInBuild = await _context.PcBuildItems.AnyAsync(x => x.ProductId == id);
            var usedInPrebuilt = await _context.PrebuiltPcComponents.AnyAsync(x => x.ComponentProductId == id);

            if (usedInBuild || usedInPrebuilt)
            {
                TempData["error"] = "Linh kiện này đang được dùng trong cấu hình hoặc PC dựng sẵn nên chưa thể xóa.";
                return RedirectToAction(nameof(Index));
            }

            if (product.Specifications.Any())
            {
                _context.ProductSpecifications.RemoveRange(product.Specifications);
            }

            if (!string.IsNullOrWhiteSpace(product.Image))
            {
                DeleteImageIfExists(product.Image);
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            TempData["success"] = "Xóa linh kiện thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetDefinitions(PcComponentType componentType)
        {
            var defs = await _context.SpecificationDefinitions
                .Where(x => x.ComponentType == componentType)
                .OrderBy(x => x.SortOrder)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Code,
                    x.DataType,
                    x.Unit,
                    x.IsRequired
                })
                .ToListAsync();

            return Json(defs);
        }

        private async Task SaveOrUpdateSpecifications(
            int productId,
            PcComponentType componentType,
            List<ProductSpecificationInputViewModel> inputSpecs)
        {
            var existingSpecs = await _context.ProductSpecifications
                .Where(x => x.ProductId == productId)
                .ToListAsync();

            var defs = await _context.SpecificationDefinitions
                .Where(x => x.ComponentType == componentType)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            var validDefIds = defs.Select(x => x.Id).ToHashSet();

            var removeOldSpecs = existingSpecs
                .Where(x => !validDefIds.Contains(x.SpecificationDefinitionId))
                .ToList();

            if (removeOldSpecs.Any())
            {
                _context.ProductSpecifications.RemoveRange(removeOldSpecs);
            }

            foreach (var def in defs)
            {
                var input = inputSpecs?.FirstOrDefault(x => x.SpecificationDefinitionId == def.Id);
                var existing = existingSpecs.FirstOrDefault(x => x.SpecificationDefinitionId == def.Id);

                if (existing == null)
                {
                    existing = new ProductSpecificationModel
                    {
                        ProductId = productId,
                        SpecificationDefinitionId = def.Id
                    };
                    _context.ProductSpecifications.Add(existing);
                }

                existing.ValueText = input?.ValueText;
                existing.ValueNumber = input?.ValueNumber;
                existing.ValueBool = input?.ValueBool;
                existing.ValueJson = input?.ValueJson;
            }

            await _context.SaveChangesAsync();
        }

        private async Task LoadDropdowns(PcComponentCreateViewModel vm)
        {
            vm.Categories = await _context.Categories
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name
                })
                .ToListAsync();

            vm.Publishers = await _context.Publishers
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name
                })
                .ToListAsync();
        }

        private async Task<string> SaveImageAsync(IFormFile file)
        {
            var folder = Path.Combine(_webHostEnvironment.WebRootPath, "media", "products");
            Directory.CreateDirectory(folder);

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(folder, fileName);

            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);

            return fileName;
        }

        private void DeleteImageIfExists(string fileName)
        {
            var path = Path.Combine(_webHostEnvironment.WebRootPath, "media", "products", fileName);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        private string GenerateSlug(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Guid.NewGuid().ToString("N")[..10];

            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            var result = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
            result = result.Replace("đ", "d");
            result = Regex.Replace(result, @"[^a-z0-9\s-]", "");
            result = Regex.Replace(result, @"\s+", "-").Trim('-');
            result = Regex.Replace(result, @"-+", "-");

            return result;
        }
    }
}