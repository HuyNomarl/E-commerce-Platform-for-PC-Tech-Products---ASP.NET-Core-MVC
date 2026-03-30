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
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Eshop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PolicyNames.CatalogManagement)]
    public class ProductController : Controller
    {
        private readonly DataContext _dataContext;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ICloudinaryService _cloudinaryService;

        public ProductController(
            IWebHostEnvironment webHostEnvironment,
            DataContext dataContext,
            ICloudinaryService cloudinaryService)
        {
            _webHostEnvironment = webHostEnvironment;
            _dataContext = dataContext;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _dataContext.Products
                .OrderByDescending(p => p.Id)
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Include(p => p.ProductImages)
                .ToListAsync();

            return View(products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            LoadViewBags();
            return View(new ProductUpsertViewModel
            {
                Product = new ProductModel()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductUpsertViewModel vm)
        {
            vm.Product ??= new ProductModel();
            LoadViewBags(vm.Product.CategoryId, vm.Product.PublisherId);

            if (!ModelState.IsValid)
            {
                TempData["error"] = "Vui lòng kiểm tra lại thông tin.";
                return View(vm);
            }

            vm.Product.Slug = GenerateSlug(vm.Product.Name);

            var slugExists = await _dataContext.Products
                .AnyAsync(p => p.Slug == vm.Product.Slug);

            if (slugExists)
            {
                ModelState.AddModelError("", "Sản phẩm đã tồn tại.");
                return View(vm);
            }

            _dataContext.Products.Add(vm.Product);
            await _dataContext.SaveChangesAsync();

            var widgetImagesInput = ParseWidgetImages(vm.WidgetImagesJson);
            var uploadedImages = MapWidgetImagesToEntities(vm.Product.Id, widgetImagesInput);

            if (uploadedImages.Any())
            {
                int primaryIndex = NormalizePrimaryNewIndex(vm.PrimaryNewImageIndex, uploadedImages.Count);
                ApplyCreateMainImage(vm.Product, uploadedImages, primaryIndex);

                _dataContext.ProductImages.AddRange(uploadedImages);
            }

            if (vm.TechnicalFileUpload != null && vm.TechnicalFileUpload.Length > 0)
            {
                var tech = await _cloudinaryService.UploadRawFileAsync(vm.TechnicalFileUpload, "eshop/technical");

                _dataContext.ProductTechnicalAssets.Add(new ProductTechnicalAssetModel
                {
                    ProductId = vm.Product.Id,
                    PublicId = tech.PublicId,
                    Url = tech.Url,
                    ResourceType = tech.ResourceType,
                    OriginalFileName = vm.TechnicalFileUpload.FileName,
                    ContentType = vm.TechnicalFileUpload.ContentType
                });
            }

            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Thêm sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _dataContext.Products
                .Include(p => p.ProductImages)
                .Include(p => p.TechnicalAsset)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            LoadViewBags(product.CategoryId, product.PublisherId);

            var vm = new ProductUpsertViewModel
            {
                Product = product,
                PrimaryImageSource = product.ProductImages != null && product.ProductImages.Any()
                    ? "existing"
                    : (!string.IsNullOrWhiteSpace(product.Image) ? "legacy" : "existing"),
                PrimaryExistingImageId = product.ProductImages?
                    .OrderBy(x => x.SortOrder)
                    .FirstOrDefault(x => x.IsMain)?.Id
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductUpsertViewModel vm)
        {
            vm.Product ??= new ProductModel();

            if (id != vm.Product.Id)
                return NotFound();

            LoadViewBags(vm.Product.CategoryId, vm.Product.PublisherId);

            var existingProduct = await _dataContext.Products
                .Include(p => p.ProductImages)
                .Include(p => p.TechnicalAsset)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingProduct == null)
                return NotFound();

            existingProduct.ProductImages ??= new List<ProductImageModel>();

            if (!ModelState.IsValid)
            {
                HydrateEditViewModelForRedisplay(vm, existingProduct);
                return View(vm);
            }

            string newSlug = GenerateSlug(vm.Product.Name);

            var slugExists = await _dataContext.Products
                .AnyAsync(p => p.Slug == newSlug && p.Id != vm.Product.Id);

            if (slugExists)
            {
                ModelState.AddModelError("", "Sản phẩm với tên này đã tồn tại!");
                HydrateEditViewModelForRedisplay(vm, existingProduct);
                return View(vm);
            }

            existingProduct.Name = vm.Product.Name;
            existingProduct.Slug = newSlug;
            existingProduct.Description = vm.Product.Description;
            existingProduct.Price = vm.Product.Price;
            existingProduct.CategoryId = vm.Product.CategoryId;
            existingProduct.PublisherId = vm.Product.PublisherId;
            existingProduct.IsPcBuild = vm.Product.IsPcBuild;

            await DeleteMarkedProductImagesAsync(existingProduct, vm.DeletedImageIds);
            await HandleLegacyImageDeletionAsync(existingProduct, vm.DeleteLegacyImage);

            var activeExistingImages = existingProduct.ProductImages
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();

            var widgetImagesInput = ParseWidgetImages(vm.WidgetImagesJson);
            var newUploadedImages = MapWidgetImagesToEntities(existingProduct.Id, widgetImagesInput);

            if (newUploadedImages.Any())
            {
                _dataContext.ProductImages.AddRange(newUploadedImages);
            }

            var allActiveImages = new List<ProductImageModel>();
            allActiveImages.AddRange(activeExistingImages);
            allActiveImages.AddRange(newUploadedImages);

            ApplyEditMainImage(existingProduct, vm, allActiveImages, newUploadedImages);

            if (vm.TechnicalFileUpload != null && vm.TechnicalFileUpload.Length > 0)
            {
                if (existingProduct.TechnicalAsset != null)
                {
                    await _cloudinaryService.DeleteAsync(
                        existingProduct.TechnicalAsset.PublicId,
                        existingProduct.TechnicalAsset.ResourceType);

                    _dataContext.ProductTechnicalAssets.Remove(existingProduct.TechnicalAsset);
                }

                var tech = await _cloudinaryService.UploadRawFileAsync(vm.TechnicalFileUpload, "eshop/technical");

                _dataContext.ProductTechnicalAssets.Add(new ProductTechnicalAssetModel
                {
                    ProductId = existingProduct.Id,
                    PublicId = tech.PublicId,
                    Url = tech.Url,
                    ResourceType = tech.ResourceType,
                    OriginalFileName = vm.TechnicalFileUpload.FileName,
                    ContentType = vm.TechnicalFileUpload.ContentType
                });
            }

            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Cập nhật sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var blockReason = await GetDeleteBlockReasonAsync(id);
            if (!string.IsNullOrWhiteSpace(blockReason))
            {
                TempData["error"] = blockReason;
                return RedirectToAction(nameof(Index));
            }

            var product = await _dataContext.Products
                .Include(p => p.ProductImages)
                .Include(p => p.TechnicalAsset)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            if (product.ProductImages != null && product.ProductImages.Any())
            {
                foreach (var image in product.ProductImages)
                {
                    if (!string.IsNullOrWhiteSpace(image.PublicId))
                    {
                        await _cloudinaryService.DeleteAsync(image.PublicId, "image");
                    }
                }

                _dataContext.ProductImages.RemoveRange(product.ProductImages);
            }

            if (product.TechnicalAsset != null)
            {
                await _cloudinaryService.DeleteAsync(
                    product.TechnicalAsset.PublicId,
                    product.TechnicalAsset.ResourceType);

                _dataContext.ProductTechnicalAssets.Remove(product.TechnicalAsset);
            }

            if (!string.IsNullOrWhiteSpace(product.Image) &&
                (product.ProductImages == null || !product.ProductImages.Any()))
            {
                await DeleteLegacyImageReferenceAsync(product.Image);
            }

            _dataContext.Products.Remove(product);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Xóa sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> AddQuantity(int id)
        {
            TempData["error"] = "Chức năng cộng trực tiếp Product.Quantity đã bị khóa. Vui lòng quản lý ở phân hệ Kho.";
            return RedirectToAction("ProductStock", "Inventory", new { area = "Admin", productId = id });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StoreProductQuantity(ProductQuantityModel productQuantityModel, int id)
        {
            TempData["error"] = "Chức năng này đã ngưng sử dụng. Vui lòng dùng phiếu nhập kho trong phân hệ Kho.";
            return RedirectToAction("ProductStock", "Inventory", new { area = "Admin", productId = id });
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

        private async Task<List<ProductImageModel>> UploadProductImagesAsync(int productId, IEnumerable<IFormFile>? files)
        {
            var result = new List<ProductImageModel>();

            if (files == null)
                return result;

            foreach (var file in files.Where(f => f != null && f.Length > 0))
            {
                var uploaded = await _cloudinaryService.UploadImageAsync(file, "eshop/products");

                result.Add(new ProductImageModel
                {
                    ProductId = productId,
                    PublicId = uploaded.PublicId,
                    Url = uploaded.Url,
                    IsMain = false,
                    SortOrder = 9999
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
                return;
            }

            var selectedMain = images[primaryIndex];
            ReOrderImages(images, selectedMain);
            product.Image = selectedMain.Url;
        }

        private void ApplyEditMainImage(
            ProductModel product,
            ProductUpsertViewModel vm,
            List<ProductImageModel> activeImages,
            List<ProductImageModel> newUploadedImages)
        {
            ProductImageModel? selectedMain = null;
            string source = (vm.PrimaryImageSource ?? "").Trim().ToLowerInvariant();

            if (source == "existing" && vm.PrimaryExistingImageId.HasValue)
            {
                selectedMain = activeImages.FirstOrDefault(x => x.Id == vm.PrimaryExistingImageId.Value);
            }
            else if (source == "new" && newUploadedImages.Any())
            {
                int newIndex = NormalizePrimaryNewIndex(vm.PrimaryNewImageIndex, newUploadedImages.Count);
                selectedMain = newUploadedImages[newIndex];
            }
            else if (source == "legacy")
            {
                // Chỉ giữ ảnh legacy làm ảnh chính khi không còn ảnh nào trong ProductImages
                if (!vm.DeleteLegacyImage && !activeImages.Any())
                {
                    product.Image = product.Image;
                    return;
                }
            }

            if (selectedMain == null && activeImages.Any())
            {
                selectedMain = activeImages
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Id)
                    .First();
            }

            ReOrderImages(activeImages, selectedMain);

            if (selectedMain != null)
            {
                product.Image = selectedMain.Url;
            }
            else
            {
                // Không còn ProductImages
                if (vm.DeleteLegacyImage)
                {
                    product.Image = null;
                }
            }
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

        private async Task DeleteMarkedProductImagesAsync(ProductModel product, IEnumerable<long>? deletedImageIds)
        {
            if (deletedImageIds == null)
                return;

            var deleteIdSet = deletedImageIds.Distinct().ToHashSet();

            if (!deleteIdSet.Any())
                return;

            var imagesToDelete = product.ProductImages
                .Where(x => deleteIdSet.Contains(x.Id))
                .ToList();

            foreach (var image in imagesToDelete)
            {
                if (!string.IsNullOrWhiteSpace(image.PublicId))
                {
                    await _cloudinaryService.DeleteAsync(image.PublicId, "image");
                }

                _dataContext.ProductImages.Remove(image);
                product.ProductImages.Remove(image);
            }
        }

        private async Task HandleLegacyImageDeletionAsync(ProductModel product, bool deleteLegacyImage)
        {
            if (!deleteLegacyImage)
                return;

            if (string.IsNullOrWhiteSpace(product.Image))
                return;

            bool imageIsStillInProductImages = product.ProductImages.Any(x => x.Url == product.Image);
            if (imageIsStillInProductImages)
                return;

            await DeleteLegacyImageReferenceAsync(product.Image);
            product.Image = null;
        }

        private async Task DeleteLegacyImageReferenceAsync(string? imageValue)
        {
            if (string.IsNullOrWhiteSpace(imageValue))
                return;

            // Nếu là link Cloudinary
            if (imageValue.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            {
                var publicId = TryExtractCloudinaryPublicId(imageValue);
                if (!string.IsNullOrWhiteSpace(publicId))
                {
                    try
                    {
                        await _cloudinaryService.DeleteAsync(publicId, "image");
                        return;
                    }
                    catch
                    {
                        // bỏ qua nếu không xóa được file cũ
                    }
                }
            }

            // Nếu là ảnh local cũ
            DeleteProductImage(imageValue);
        }

        private string? TryExtractCloudinaryPublicId(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            string path = uri.AbsolutePath;
            const string marker = "/upload/";
            int markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (markerIndex < 0)
                return null;

            string tail = path[(markerIndex + marker.Length)..].Trim('/');
            var parts = tail.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

            if (!parts.Any())
                return null;

            int versionIndex = parts.FindIndex(p => Regex.IsMatch(p, @"^v\d+$"));
            if (versionIndex >= 0 && versionIndex < parts.Count - 1)
            {
                parts = parts.Skip(versionIndex + 1).ToList();
            }

            if (!parts.Any())
                return null;

            string last = parts[^1];
            int dotIndex = last.LastIndexOf('.');
            if (dotIndex > 0)
            {
                parts[^1] = last[..dotIndex];
            }

            return string.Join("/", parts);
        }

        private void HydrateEditViewModelForRedisplay(ProductUpsertViewModel vm, ProductModel existingProduct)
        {
            vm.Product.Image = existingProduct.Image;
            vm.Product.Slug = existingProduct.Slug;
            vm.Product.ProductImages = existingProduct.ProductImages
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();
            vm.Product.TechnicalAsset = existingProduct.TechnicalAsset;
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

        private void DeleteProductImage(string imageNameOrUrl)
        {
            if (string.IsNullOrWhiteSpace(imageNameOrUrl))
                return;

            string fileName = Path.GetFileName(imageNameOrUrl);
            string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "media/products");
            string filePath = Path.Combine(uploadDir, fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
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

        private async Task<string?> GetDeleteBlockReasonAsync(int productId)
        {
            if (await _dataContext.InventoryStocks.AnyAsync(x =>
                x.ProductId == productId &&
                (x.OnHandQuantity > 0 || x.ReservedQuantity > 0)))
            {
                return "Sản phẩm đang còn tồn hoặc đang được giữ chỗ trong kho.";
            }

            if (await _dataContext.InventoryReservationDetails.AnyAsync(x =>
                x.ProductId == productId &&
                x.InventoryReservation.Status == InventoryReservationStatus.Active))
            {
                return "Sản phẩm đang nằm trong reservation hoạt động.";
            }

            if (await _dataContext.InventoryTransactionDetails.AnyAsync(x => x.ProductId == productId))
            {
                return "Sản phẩm đã có lịch sử nhập/xuất kho nên không được xóa cứng.";
            }

            if (await _dataContext.OrderDetails.AnyAsync(x => x.ProductId == productId))
            {
                return "Sản phẩm đã phát sinh đơn hàng nên không được xóa.";
            }

            if (await _dataContext.PrebuiltPcComponents.AnyAsync(x => x.ProductId == productId || x.ComponentProductId == productId))
            {
                return "Sản phẩm đang được dùng trong PC dựng sẵn.";
            }

            if (await _dataContext.PcBuildItems.AnyAsync(x => x.ProductId == productId))
            {
                return "Sản phẩm đang nằm trong build PC đã lưu.";
            }

            return null;
        }

    }
}
