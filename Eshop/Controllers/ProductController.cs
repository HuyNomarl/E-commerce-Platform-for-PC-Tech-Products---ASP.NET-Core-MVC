using Eshop.Areas.Admin.Models;
using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Eshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace Eshop.Controllers
{
    public class ProductController : Controller
    {
        private const int MaxReviewMediaFiles = 8;
        private const long MaxReviewImageSizeBytes = 5 * 1024 * 1024;
        private const long MaxReviewVideoSizeBytes = 50 * 1024 * 1024;

        private readonly DataContext _dataContext;
        private readonly UserManager<AppUserModel> _userManager;
        private readonly ICloudinaryService _cloudinaryService;

        public ProductController(
            DataContext dataContext,
            UserManager<AppUserModel> userManager,
            ICloudinaryService cloudinaryService)
        {
            _dataContext = dataContext;
            _userManager = userManager;
            _cloudinaryService = cloudinaryService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _dataContext.Products
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Include(p => p.ProductImages)
                .Include(p => p.TechnicalAsset)
                .Include(p => p.Specifications)
                    .ThenInclude(s => s.SpecificationDefinition)
                .Include(p => p.RatingModel)
                    .ThenInclude(r => r.MediaItems)
                .Include(p => p.OptionGroups)
                    .ThenInclude(g => g.OptionValues)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return RedirectToAction(nameof(Index));

            var relatedProducts = await _dataContext.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Publisher)
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .OrderByDescending(p => p.Sold)
                .ThenByDescending(p => p.Id)
                .Take(4)
                .ToListAsync();

            var totalOnHand = await _dataContext.InventoryStocks
                .Where(x => x.ProductId == id)
                .SumAsync(x => (int?)x.OnHandQuantity) ?? 0;

            var totalReserved = await _dataContext.InventoryStocks
                .Where(x => x.ProductId == id)
                .SumAsync(x => (int?)x.ReservedQuantity) ?? 0;

            var currentUser = await _userManager.GetUserAsync(User);
            var existingReview = currentUser == null
                ? null
                : product.RatingModel.FirstOrDefault(x => x.UserId == currentUser.Id);

            bool hasPurchasedProduct = currentUser != null
                && await HasUserPurchasedProductAsync(currentUser.Id, id);

            var reviewSummary = BuildReviewSummary(product.RatingModel);

            var viewModel = new ProductDetailViewModel
            {
                ProductDetail = product,
                ProductId = product.Id,
                Quantity = 1,
                InventorySummary = new ProductInventorySummaryViewModel
                {
                    ProductId = id,
                    TotalOnHand = totalOnHand,
                    TotalReserved = totalReserved
                },
                GalleryItems = BuildGalleryItems(product),
                Specifications = BuildSpecifications(product),
                RelatedProducts = relatedProducts,
                Reviews = product.RatingModel
                    .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .ToList(),
                ReviewSummary = reviewSummary,
                ReviewForm = new ProductReviewFormViewModel
                {
                    ProductId = product.Id,
                    Stars = existingReview?.Stars ?? (reviewSummary.TotalReviews > 0
                        ? (int)Math.Round(reviewSummary.AverageStars, MidpointRounding.AwayFromZero)
                        : 5),
                    Comment = existingReview?.Comment
                },
                IsAuthenticated = currentUser != null,
                HasPurchasedProduct = hasPurchasedProduct,
                HasExistingReview = existingReview != null,
                CurrentUserDisplayName = currentUser?.UserName,
                CurrentUserEmail = currentUser?.Email,
                TechnicalDocumentUrl = product.TechnicalAsset?.Url,
                TechnicalDocumentName = product.TechnicalAsset?.OriginalFileName
            };

            viewModel.ReviewPermissionMessage = currentUser == null
                ? "Đăng nhập để đánh giá sản phẩm."
                : hasPurchasedProduct
                    ? (existingReview != null
                        ? "Bạn đã từng đánh giá sản phẩm này. Gửi lại biểu mẫu để cập nhật nội dung và bổ sung media mới."
                        : "Bạn có thể gửi đánh giá vì đã mua sản phẩm này.")
                    : "Chỉ khách đã mua và nhận hàng mới có thể đánh giá. Bạn vẫn có thể xem toàn bộ review.";

            return View(viewModel);
        }

        public async Task<IActionResult> Search(string searchTerm)
        {
            IQueryable<ProductModel> query = _dataContext.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Publisher);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p =>
                    p.Name.Contains(searchTerm) ||
                    p.Description.Contains(searchTerm));
            }

            var products = await query
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            ViewBag.Keyword = searchTerm;
            return View(products);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CommentProduct(ProductReviewFormViewModel reviewForm)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var productExists = await _dataContext.Products
                .AsNoTracking()
                .AnyAsync(p => p.Id == reviewForm.ProductId);

            if (!productExists)
            {
                TempData["error"] = "Sản phẩm không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            var existingReview = await _dataContext.RatingModels
                .Include(x => x.MediaItems)
                .FirstOrDefaultAsync(x =>
                    x.UserId == currentUser.Id &&
                    x.ProductId == reviewForm.ProductId);

            if (!await HasUserPurchasedProductAsync(currentUser.Id, reviewForm.ProductId))
            {
                TempData["error"] = "Chỉ khách đã mua và nhận hàng mới có thể đánh giá sản phẩm.";
                return RedirectToAction(nameof(Details), new { id = reviewForm.ProductId });
            }

            var incomingFiles = (reviewForm.MediaFiles ?? new List<IFormFile>())
                .Where(x => x != null && x.Length > 0)
                .ToList();

            var validationError = ValidateReview(reviewForm, existingReview, incomingFiles);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                TempData["error"] = validationError;
                return RedirectToAction(nameof(Details), new { id = reviewForm.ProductId });
            }

            var uploadedMedia = new List<CloudinaryUploadResultViewModel>();

            try
            {
                foreach (var file in incomingFiles)
                {
                    CloudinaryUploadResultViewModel uploaded;

                    if (IsImageFile(file))
                    {
                        uploaded = await _cloudinaryService.UploadImageAsync(file, "eshop/reviews/images");
                    }
                    else if (IsVideoFile(file))
                    {
                        uploaded = await _cloudinaryService.UploadVideoAsync(file, "eshop/reviews/videos");
                    }
                    else
                    {
                        throw new InvalidOperationException("File media không hợp lệ.");
                    }

                    uploaded.OriginalFileName = file.FileName;
                    uploadedMedia.Add(uploaded);
                }

                await using var transaction = await _dataContext.Database.BeginTransactionAsync();

                bool isNewReview = existingReview == null;

                if (existingReview == null)
                {
                    existingReview = new RatingModel
                    {
                        ProductId = reviewForm.ProductId,
                        UserId = currentUser.Id,
                        Name = ResolveDisplayName(currentUser),
                        Email = currentUser.Email,
                        Comment = NormalizeReviewComment(reviewForm.Comment),
                        Stars = reviewForm.Stars,
                        CreatedAt = DateTime.Now
                    };

                    _dataContext.RatingModels.Add(existingReview);
                    await _dataContext.SaveChangesAsync();
                }
                else
                {
                    existingReview.Name = ResolveDisplayName(currentUser);
                    existingReview.Email = currentUser.Email;
                    existingReview.Comment = NormalizeReviewComment(reviewForm.Comment);
                    existingReview.Stars = reviewForm.Stars;
                    existingReview.UpdatedAt = DateTime.Now;

                    await _dataContext.SaveChangesAsync();
                }

                if (uploadedMedia.Any())
                {
                    int currentSort = existingReview.MediaItems.Count;

                    foreach (var item in uploadedMedia)
                    {
                        _dataContext.RatingMedia.Add(new RatingMediaModel
                        {
                            RatingId = existingReview.Id,
                            PublicId = item.PublicId,
                            Url = item.Url,
                            ResourceType = item.ResourceType,
                            OriginalFileName = item.OriginalFileName,
                            SortOrder = currentSort++
                        });
                    }

                    await _dataContext.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                TempData["success"] = isNewReview
                    ? "Đã gửi đánh giá thành công."
                    : "Đã cập nhật đánh giá thành công.";
            }
            catch (Exception ex)
            {
                foreach (var item in uploadedMedia)
                {
                    try
                    {
                        await _cloudinaryService.DeleteAsync(item.PublicId, item.ResourceType);
                    }
                    catch
                    {
                    }
                }

                TempData["error"] = $"Không thể gửi đánh giá: {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = reviewForm.ProductId });
        }

        private async Task<bool> HasUserPurchasedProductAsync(string userId, int productId)
        {
            return await _dataContext.OrderDetails
                .AnyAsync(x =>
                    x.ProductId == productId &&
                    x.Order.UserId == userId &&
                    (x.Order.Status == (int)OrderStatus.Delivered ||
                     x.Order.Status == (int)OrderStatus.Completed));
        }

        private static string? ValidateReview(
            ProductReviewFormViewModel reviewForm,
            RatingModel? existingReview,
            List<IFormFile> incomingFiles)
        {
            if (reviewForm.Stars < 1 || reviewForm.Stars > 5)
                return "Vui lòng chọn số sao hợp lệ.";

            int currentMediaCount = existingReview?.MediaItems.Count ?? 0;

            if (currentMediaCount + incomingFiles.Count > MaxReviewMediaFiles)
            {
                return $"Mỗi đánh giá chỉ được tối đa {MaxReviewMediaFiles} media.";
            }

            bool hasComment = !string.IsNullOrWhiteSpace(reviewForm.Comment);
            bool hasIncomingMedia = incomingFiles.Any();
            bool hasExistingMedia = existingReview?.MediaItems.Any() == true;

            if (!hasComment && !hasIncomingMedia && !hasExistingMedia)
            {
                return "Hãy nhập nội dung đánh giá hoặc tải lên ít nhất một ảnh/video.";
            }

            foreach (var file in incomingFiles)
            {
                if (!IsImageFile(file) && !IsVideoFile(file))
                {
                    return "Chỉ hỗ trợ ảnh hoặc video trong phần đánh giá.";
                }

                if (IsImageFile(file) && file.Length > MaxReviewImageSizeBytes)
                {
                    return "Mỗi ảnh đánh giá chỉ được tối đa 5MB.";
                }

                if (IsVideoFile(file) && file.Length > MaxReviewVideoSizeBytes)
                {
                    return "Mỗi video đánh giá chỉ được tối đa 50MB.";
                }
            }

            return null;
        }

        private static List<ProductGalleryItemViewModel> BuildGalleryItems(ProductModel product)
        {
            var items = new List<ProductGalleryItemViewModel>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var image in product.ProductImages
                .OrderBy(x => x.SortOrder)
                .ThenByDescending(x => x.IsMain)
                .ThenBy(x => x.Id))
            {
                var url = ResolveProductAssetUrl(image.Url);
                if (string.IsNullOrWhiteSpace(url) || !seen.Add(url))
                    continue;

                items.Add(new ProductGalleryItemViewModel
                {
                    Url = url,
                    AltText = product.Name,
                    IsMain = image.IsMain
                });
            }

            var legacyImageUrl = ResolveProductAssetUrl(product.Image);
            if (!string.IsNullOrWhiteSpace(legacyImageUrl) && seen.Add(legacyImageUrl))
            {
                items.Add(new ProductGalleryItemViewModel
                {
                    Url = legacyImageUrl,
                    AltText = product.Name,
                    IsMain = !items.Any()
                });
            }

            if (items.Any() && !items.Any(x => x.IsMain))
            {
                items[0].IsMain = true;
            }

            return items;
        }

        private static List<ProductSpecificationDisplayViewModel> BuildSpecifications(ProductModel product)
        {
            return (product.Specifications ?? new List<ProductSpecificationModel>())
                .Where(x => x.SpecificationDefinition != null)
                .OrderBy(x => x.SpecificationDefinition.SortOrder)
                .ThenBy(x => x.SpecificationDefinition.Name)
                .Select(x => new ProductSpecificationDisplayViewModel
                {
                    Name = x.SpecificationDefinition.Name,
                    Value = FormatSpecificationValue(x)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .ToList();
        }

        private static ProductReviewSummaryViewModel BuildReviewSummary(IEnumerable<RatingModel> reviews)
        {
            var reviewList = reviews?.ToList() ?? new List<RatingModel>();

            var summary = new ProductReviewSummaryViewModel
            {
                TotalReviews = reviewList.Count,
                AverageStars = (decimal)(reviewList.Any()
                    ? Math.Round(reviewList.Average(x => x.Stars), 1, MidpointRounding.AwayFromZero)
                    : 0)
            };

            for (int star = 5; star >= 1; star--)
            {
                summary.CountByStars[star] = reviewList.Count(x => x.Stars == star);
            }

            return summary;
        }

        private static string FormatSpecificationValue(ProductSpecificationModel spec)
        {
            if (!string.IsNullOrWhiteSpace(spec.ValueText))
            {
                return spec.ValueText.Trim();
            }

            if (spec.ValueNumber.HasValue)
            {
                var formattedNumber = spec.ValueNumber.Value % 1 == 0
                    ? spec.ValueNumber.Value.ToString("0", CultureInfo.InvariantCulture)
                    : spec.ValueNumber.Value.ToString("0.##", CultureInfo.InvariantCulture);

                return string.IsNullOrWhiteSpace(spec.SpecificationDefinition.Unit)
                    ? formattedNumber
                    : $"{formattedNumber} {spec.SpecificationDefinition.Unit}";
            }

            if (spec.ValueBool.HasValue)
            {
                return spec.ValueBool.Value ? "Có" : "Không";
            }

            if (!string.IsNullOrWhiteSpace(spec.ValueJson))
            {
                try
                {
                    var jsonValues = JsonSerializer.Deserialize<List<string>>(spec.ValueJson) ?? new List<string>();
                    return string.Join(", ", jsonValues.Where(x => !string.IsNullOrWhiteSpace(x)));
                }
                catch
                {
                    return spec.ValueJson;
                }
            }

            return string.Empty;
        }

        private static string? ResolveProductAssetUrl(string? assetValue)
        {
            if (string.IsNullOrWhiteSpace(assetValue))
                return null;

            if (assetValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                assetValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return assetValue;
            }

            return "/media/products/" + Path.GetFileName(assetValue);
        }

        private static bool IsImageFile(IFormFile file)
        {
            return file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVideoFile(IFormFile file)
        {
            return file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDisplayName(AppUserModel user)
        {
            return string.IsNullOrWhiteSpace(user.UserName)
                ? user.Email ?? "Khách hàng"
                : user.UserName;
        }

        private static string? NormalizeReviewComment(string? comment)
        {
            return string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        }
    }
}
