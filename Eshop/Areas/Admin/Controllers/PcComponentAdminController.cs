using Eshop.Areas.Admin.Models.ViewModels;
using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Eshop.Services;
using Eshop.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PolicyNames.CatalogManagement)]
    public class PcComponentAdminController : Controller
    {
        private readonly DataContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public PcComponentAdminController(
            DataContext context,
            ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _context.Products
                .Include(x => x.Publisher)
                .Include(x => x.Category)
                .Include(x => x.ProductImages)
                .Where(x => x.ProductType == ProductType.Component || x.ProductType == ProductType.Monitor)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Create(PcComponentType? componentType)
        {
            var vm = new PcComponentCreateViewModel
            {
                Product = new ProductModel
                {
                    ProductType = componentType == PcComponentType.Monitor
                        ? ProductType.Monitor
                        : ProductType.Component,
                    ComponentType = componentType,
                    Quantity = 1,
                    Price = 1
                },
                Specifications = new List<ProductSpecificationInputViewModel>(),
                PrimaryImageSource = "new"
            };

            await LoadFormData(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PcComponentCreateViewModel vm)
        {
            vm.Product ??= new ProductModel();
            vm.Specifications ??= new List<ProductSpecificationInputViewModel>();
            vm.ImageUploads ??= new List<IFormFile>();
            vm.WidgetImagesJson ??= string.Empty;

            vm.Product.Quantity = 0;

            await LoadFormData(vm);

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

            var newSlug = GenerateSlug(vm.Product.Name);

            var slugExists = await _context.Products.AnyAsync(x => x.Slug == newSlug);
            if (slugExists)
            {
                ModelState.AddModelError("Product.Name", "Tên sản phẩm đã tồn tại.");
                return View(vm);
            }

            vm.Product.Slug = newSlug;

            _context.Products.Add(vm.Product);
            await _context.SaveChangesAsync();

            // Lấy ảnh từ Cloudinary Widget
            var widgetImagesInput = ParseWidgetImages(vm.WidgetImagesJson);
            var uploadedImages = MapWidgetImagesToEntities(vm.Product.Id, widgetImagesInput);

            if (uploadedImages.Any())
            {
                int primaryIndex = NormalizePrimaryNewIndex(vm.PrimaryNewImageIndex, uploadedImages.Count);
                ApplyCreateMainImage(vm.Product, uploadedImages, primaryIndex);

                var mainImage = uploadedImages.FirstOrDefault(x => x.IsMain);
                vm.Product.Image = mainImage?.Url;
                vm.Product.ImagePublicId = mainImage?.PublicId;

                _context.ProductImages.AddRange(uploadedImages);
            }
            else
            {
                vm.Product.Image = null;
                vm.Product.ImagePublicId = null;
            }

            await SaveOrUpdateSpecifications(
                vm.Product.Id,
                vm.Product.ComponentType!.Value,
                vm.Specifications);

            _context.Products.Update(vm.Product);
            await _context.SaveChangesAsync();

            TempData["success"] = "Tạo linh kiện thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .Include(x => x.Specifications)
                .Include(x => x.ProductImages)
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
                Product = product,
                Specifications = new List<ProductSpecificationInputViewModel>()
            };

            if (product.ComponentType.HasValue && product.ComponentType.Value != PcComponentType.None)
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

            MapExistingImages(vm, product);
            await LoadFormData(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PcComponentCreateViewModel vm)
        {
            vm.Product ??= new ProductModel();
            vm.Specifications ??= new List<ProductSpecificationInputViewModel>();
            vm.ImageUploads ??= new List<IFormFile>();
            vm.WidgetImagesJson ??= string.Empty;
            vm.DeletedImageIds ??= new List<int>();

            await LoadFormData(vm);

            var product = await _context.Products
                .Include(x => x.Specifications)
                .Include(x => x.ProductImages)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            if (product.ComponentType != vm.Product.ComponentType)
            {
                var locked = await HasLockedInventoryHistoryAsync(id);
                if (locked)
                {
                    ModelState.AddModelError("Product.ComponentType", "Không thể đổi loại linh kiện khi đã có tồn kho hoặc lịch sử bán hàng.");
                }
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
                MapExistingImages(vm, product);
                return View(vm);
            }

            var newSlug = GenerateSlug(vm.Product.Name);

            var slugExists = await _context.Products
                .AnyAsync(x => x.Slug == newSlug && x.Id != id);

            if (slugExists)
            {
                ModelState.AddModelError("Product.Name", "Tên sản phẩm đã tồn tại.");
                MapExistingImages(vm, product);
                return View(vm);
            }

            product.Name = vm.Product.Name;
            product.Description = vm.Product.Description;
            product.Price = vm.Product.Price;
            product.CategoryId = vm.Product.CategoryId;
            product.PublisherId = vm.Product.PublisherId;
            product.ComponentType = vm.Product.ComponentType;
            product.ProductType = vm.Product.ProductType;
            product.IsPcBuild = false;
            product.Slug = newSlug;

            var hasLegacyImage = !string.IsNullOrWhiteSpace(product.Image)
                && !product.ProductImages.Any(x => x.Url == product.Image);

            var imagesToDelete = product.ProductImages
                .Where(x => vm.DeletedImageIds.Contains(x.Id))
                .ToList();

            foreach (var img in imagesToDelete)
            {
                if (!string.IsNullOrWhiteSpace(img.PublicId))
                {
                    await _cloudinaryService.DeleteAsync(img.PublicId, "image");
                }
            }

            if (imagesToDelete.Any())
            {
                _context.ProductImages.RemoveRange(imagesToDelete);
            }

            var remainingExistingImages = product.ProductImages
                .Where(x => !vm.DeletedImageIds.Contains(x.Id))
                .OrderBy(x => x.SortOrder)
                .ToList();

            if (vm.DeleteLegacyImage && hasLegacyImage)
            {
                if (!string.IsNullOrWhiteSpace(product.ImagePublicId))
                {
                    await _cloudinaryService.DeleteAsync(product.ImagePublicId, "image");
                }

                product.Image = null;
                product.ImagePublicId = null;
            }

            var nextSortOrder = remainingExistingImages.Any()
       ? remainingExistingImages.Max(x => x.SortOrder) + 1
       : 0;

            var widgetImagesInput = ParseWidgetImages(vm.WidgetImagesJson);
            var newImages = MapWidgetImagesToEntities(product.Id, widgetImagesInput, nextSortOrder);

            if (newImages.Any())
            {
                _context.ProductImages.AddRange(newImages);
            }

            foreach (var img in remainingExistingImages)
            {
                img.IsMain = false;
            }

            foreach (var img in newImages)
            {
                img.IsMain = false;
            }

            ProductImageModel? selectedMainImage = null;
            var keepLegacyAsMain = false;

            if (vm.PrimaryImageSource == "existing" && vm.PrimaryExistingImageId.HasValue)
            {
                selectedMainImage = remainingExistingImages
                    .FirstOrDefault(x => x.Id == vm.PrimaryExistingImageId.Value);
            }
            else if (vm.PrimaryImageSource == "new" && vm.PrimaryNewImageIndex.HasValue)
            {
                var idx = vm.PrimaryNewImageIndex.Value;
                if (idx >= 0 && idx < newImages.Count)
                {
                    selectedMainImage = newImages[idx];
                }
            }
            else if (vm.PrimaryImageSource == "legacy" && hasLegacyImage && !vm.DeleteLegacyImage)
            {
                keepLegacyAsMain = true;
            }

            if (!keepLegacyAsMain && selectedMainImage == null)
            {
                selectedMainImage = remainingExistingImages
                    .Concat(newImages)
                    .OrderBy(x => x.SortOrder)
                    .FirstOrDefault();
            }

            if (selectedMainImage != null)
            {
                selectedMainImage.IsMain = true;
                product.Image = selectedMainImage.Url;
                product.ImagePublicId = selectedMainImage.PublicId;
            }
            else if (keepLegacyAsMain)
            {
                foreach (var img in remainingExistingImages)
                {
                    img.IsMain = false;
                }

                foreach (var img in newImages)
                {
                    img.IsMain = false;
                }
            }
            else
            {
                product.Image = null;
                product.ImagePublicId = null;
            }

            await SaveOrUpdateSpecifications(
                product.Id,
                product.ComponentType!.Value,
                vm.Specifications);

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

            var deleteReason = await GetDeleteBlockReasonAsync(id);
            if (!string.IsNullOrWhiteSpace(deleteReason))
            {
                TempData["error"] = deleteReason;
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

            if (!string.IsNullOrWhiteSpace(product.ImagePublicId))
            {
                await _cloudinaryService.DeleteAsync(product.ImagePublicId, "image");
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
                var input = inputSpecs.FirstOrDefault(x => x.SpecificationDefinitionId == def.Id);
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
        }

        private async Task LoadFormData(PcComponentCreateViewModel vm)
        {
            await LoadDropdowns(vm);

            vm.Product ??= new ProductModel();
            vm.Specifications ??= new List<ProductSpecificationInputViewModel>();

            if (!vm.Product.ComponentType.HasValue || vm.Product.ComponentType.Value == PcComponentType.None)
            {
                return;
            }

            var defs = await _context.SpecificationDefinitions
                .Where(x => x.ComponentType == vm.Product.ComponentType.Value)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            var currentSpecs = vm.Specifications ?? new List<ProductSpecificationInputViewModel>();

            vm.Specifications = defs.Select(def =>
            {
                var oldValue = currentSpecs.FirstOrDefault(x => x.SpecificationDefinitionId == def.Id);

                return new ProductSpecificationInputViewModel
                {
                    SpecificationDefinitionId = def.Id,
                    Name = def.Name,
                    Code = def.Code,
                    DataType = def.DataType,
                    Unit = def.Unit,
                    ValueText = oldValue?.ValueText,
                    ValueNumber = oldValue?.ValueNumber,
                    ValueBool = oldValue?.ValueBool,
                    ValueJson = oldValue?.ValueJson
                };
            }).ToList();
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

        private async Task<List<(string Url, string PublicId)>> UploadComponentImagesAsync(IEnumerable<IFormFile> files)
        {
            var result = new List<(string Url, string PublicId)>();

            foreach (var file in files ?? Enumerable.Empty<IFormFile>())
            {
                if (file == null || file.Length <= 0) continue;

                var uploaded = await _cloudinaryService.UploadImageAsync(file, "eshop/components");
                result.Add((uploaded.Url, uploaded.PublicId));
            }

            return result;
        }
        private void MapExistingImages(PcComponentCreateViewModel vm, ProductModel product)
        {
            vm.ExistingImages = product.ProductImages?
                .OrderBy(x => x.SortOrder)
                .Select(x => new EditableProductImageViewModel
                {
                    Id = x.Id,
                    Url = x.Url,
                    IsMain = x.IsMain
                })
                .ToList() ?? new List<EditableProductImageViewModel>();
        }

        private async Task<List<ProductImageModel>> UploadComponentImagesAsync(
            int productId,
            IEnumerable<IFormFile> files,
            int startSortOrder = 0)
        {
            var result = new List<ProductImageModel>();
            var sortOrder = startSortOrder;

            foreach (var file in files ?? Enumerable.Empty<IFormFile>())
            {
                if (file == null || file.Length <= 0) continue;

                var uploaded = await _cloudinaryService.UploadImageAsync(file, "eshop/components");

                result.Add(new ProductImageModel
                {
                    ProductId = productId,
                    Url = uploaded.Url,
                    PublicId = uploaded.PublicId,
                    SortOrder = sortOrder++,
                    IsMain = false
                });
            }

            return result;
        }

        private int NormalizePrimaryNewIndex(int? requestedIndex, int totalCount)
        {
            if (totalCount <= 0)
                return 0;

            if (!requestedIndex.HasValue)
                return 0;

            if (requestedIndex.Value < 0 || requestedIndex.Value >= totalCount)
                return 0;

            return requestedIndex.Value;
        }

        private void ApplyCreateMainImage(ProductModel product, List<ProductImageModel> images, int primaryIndex)
        {
            if (images == null || images.Count == 0)
            {
                product.Image = null;
                product.ImagePublicId = null;
                return;
            }

            var selectedMain = images[primaryIndex];
            ReOrderImages(images, selectedMain);

            product.Image = selectedMain.Url;
            product.ImagePublicId = selectedMain.PublicId;
        }

        private void ReOrderImages(List<ProductImageModel> images, ProductImageModel? selectedMain)
        {
            if (images == null || images.Count == 0)
                return;

            foreach (var img in images)
            {
                img.IsMain = false;
            }

            var ordered = images
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();

            if (selectedMain != null)
            {
                ordered.Remove(selectedMain);
                ordered.Insert(0, selectedMain);
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].SortOrder = i;
                ordered[i].IsMain = i == 0;
            }
        }

        private List<WidgetUploadedImageInputViewModel> ParseWidgetImages(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<WidgetUploadedImageInputViewModel>();

            try
            {
                return JsonSerializer.Deserialize<List<WidgetUploadedImageInputViewModel>>(json,
                           new JsonSerializerOptions
                           {
                               PropertyNameCaseInsensitive = true
                           })
                       ?? new List<WidgetUploadedImageInputViewModel>();
            }
            catch
            {
                return new List<WidgetUploadedImageInputViewModel>();
            }
        }

        private List<ProductImageModel> MapWidgetImagesToEntities(
            int productId,
            IEnumerable<WidgetUploadedImageInputViewModel> widgetImages)
        {
            return widgetImages
                .Where(x => !string.IsNullOrWhiteSpace(x.PublicId) && !string.IsNullOrWhiteSpace(x.Url))
                .Select(x => new ProductImageModel
                {
                    ProductId = productId,
                    PublicId = x.PublicId,
                    Url = x.Url,
                    IsMain = false,
                    SortOrder = 9999
                })
                .ToList();
        }

        private List<ProductImageModel> MapWidgetImagesToEntities(
                int productId,
                IEnumerable<WidgetUploadedImageInputViewModel> widgetImages,
                int startSortOrder = 0)
        {
            var result = new List<ProductImageModel>();
            var sortOrder = startSortOrder;

            foreach (var x in widgetImages.Where(x =>
                         !string.IsNullOrWhiteSpace(x.PublicId) &&
                         !string.IsNullOrWhiteSpace(x.Url)))
            {
                result.Add(new ProductImageModel
                {
                    ProductId = productId,
                    PublicId = x.PublicId,
                    Url = x.Url,
                    IsMain = false,
                    SortOrder = sortOrder++
                });
            }

            return result;
        }

        private async Task<string?> GetDeleteBlockReasonAsync(int productId)
        {
            if (await _context.InventoryStocks.AnyAsync(x =>
                x.ProductId == productId &&
                (x.OnHandQuantity > 0 || x.ReservedQuantity > 0)))
            {
                return "Linh kiện đang còn tồn hoặc đang được giữ chỗ trong kho.";
            }

            if (await _context.InventoryReservationDetails.AnyAsync(x =>
                x.ProductId == productId &&
                x.InventoryReservation.Status == InventoryReservationStatus.Active))
            {
                return "Linh kiện đang nằm trong reservation hoạt động.";
            }

            if (await _context.InventoryTransactionDetails.AnyAsync(x => x.ProductId == productId))
            {
                return "Linh kiện đã có lịch sử nhập/xuất kho nên không được xóa cứng.";
            }

            if (await _context.OrderDetails.AnyAsync(x => x.ProductId == productId))
            {
                return "Linh kiện đã phát sinh đơn hàng nên không được xóa.";
            }

            if (await _context.PcBuildItems.AnyAsync(x => x.ProductId == productId))
            {
                return "Linh kiện đang được dùng trong build PC đã lưu.";
            }

            if (await _context.PrebuiltPcComponents.AnyAsync(x => x.ComponentProductId == productId || x.ProductId == productId))
            {
                return "Linh kiện đang được dùng trong PC dựng sẵn.";
            }

            return null;
        }

        private async Task<bool> HasLockedInventoryHistoryAsync(int productId)
        {
            return await _context.InventoryStocks.AnyAsync(x =>
                       x.ProductId == productId &&
                       (x.OnHandQuantity > 0 || x.ReservedQuantity > 0))
                   || await _context.InventoryTransactionDetails.AnyAsync(x => x.ProductId == productId)
                   || await _context.OrderDetails.AnyAsync(x => x.ProductId == productId);
        }

    }
}
