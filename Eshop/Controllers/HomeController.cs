using Eshop.Constants;
using Eshop.Helpers;
using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Repository;
using Eshop.Services;
using Eshop.Views.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Eshop.Controllers
{
    public class HomeController : Controller
    {
        private const int MaxCompareItems = 4;

        private sealed class CompareGroupInfo
        {
            public CompareGroupInfo(string key, string label)
            {
                Key = key;
                Label = label;
            }

            public string Key { get; }
            public string Label { get; }
        }

        private readonly DataContext _dataContext;
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<AppUserModel> _userManager;
        private readonly IMemoryCache _memoryCache;
        private readonly RecommendationPredictService _recommendService;

        public HomeController(
            ILogger<HomeController> logger,
            DataContext dataContext,
            UserManager<AppUserModel> userManager,
            IMemoryCache memoryCache,
            RecommendationPredictService recommendService)
        {
            _logger = logger;
            _dataContext = dataContext;
            _userManager = userManager;
            _memoryCache = memoryCache;
            _recommendService = recommendService;
        }

        public async Task<IActionResult> Index()
        {
            if (!_memoryCache.TryGetValue(CacheKeys.HomeProducts, out List<ProductModel>? products))
            {
                _logger.LogInformation("CACHE MISS: HomeProducts");

                products = await _dataContext.Products
                    .AsNoTracking()
                    .Include(p => p.Publisher)
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .WhereVisibleOnStorefront(_dataContext)
                    .ToListAsync();

                var productCacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));

                _memoryCache.Set(CacheKeys.HomeProducts, products, productCacheOptions);
            }
            else
            {
                _logger.LogInformation("CACHE HIT: HomeProducts");
            }

            if (!_memoryCache.TryGetValue(CacheKeys.HomeSliders, out List<SliderModel>? sliders))
            {
                _logger.LogInformation("CACHE MISS: HomeSliders");

                sliders = await _dataContext.Sliders
                    .AsNoTracking()
                    .Where(p => p.Status == 1)
                    .ToListAsync();

                var sliderCacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                _memoryCache.Set(CacheKeys.HomeSliders, sliders, sliderCacheOptions);
            }
            else
            {
                _logger.LogInformation("CACHE HIT: HomeSliders");
            }

            List<ProductModel> recommendedProducts = new();

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                recommendedProducts = await _recommendService.RecommendAsync(user.Id, 8);
            }

            ViewBag.Sliders = sliders;
            ViewBag.RecommendedProducts = recommendedProducts;

            return View(products);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddWishlist(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Vui lòng đăng nhập để sử dụng wishlist."
                });
            }

            if (!User.CanUseWishlistAndCompare())
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "Chỉ tài khoản khách hàng mới được dùng wishlist."
                });
            }

            var targetProduct = await _dataContext.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .WhereVisibleOnStorefront(_dataContext)
                .FirstOrDefaultAsync(x => x.Id == productId);

            if (targetProduct == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Sản phẩm không tồn tại."
                });
            }

            bool existed = await _dataContext.Wishlists
                .AnyAsync(x => x.UserId == user.Id && x.ProductId == productId);

            if (existed)
            {
                return Ok(new
                {
                    success = true,
                    message = "Sản phẩm đã có trong wishlist."
                });
            }

            var wishlist = new WishlistModel
            {
                ProductId = productId,
                UserId = user.Id
            };

            _dataContext.Wishlists.Add(wishlist);
            await _dataContext.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đã thêm sản phẩm vào wishlist."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCompare(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Vui lòng đăng nhập để sử dụng compare."
                });
            }

            if (!User.CanUseWishlistAndCompare())
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "Chỉ tài khoản khách hàng mới được dùng so sánh sản phẩm."
                });
            }

            var targetProduct = await _dataContext.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .WhereVisibleOnStorefront(_dataContext)
                .FirstOrDefaultAsync(x => x.Id == productId);

            if (targetProduct == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Sản phẩm không tồn tại."
                });
            }

            bool existed = await _dataContext.Compares
                .AnyAsync(x => x.UserId == user.Id && x.ProductId == productId);

            if (existed)
            {
                return Ok(new
                {
                    success = true,
                    message = "Sản phẩm đã có trong danh sách so sánh."
                });
            }

            var currentCompareProducts = await _dataContext.Compares
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .Include(x => x.Product)
                    .ThenInclude(x => x.Category)
                .Select(x => x.Product)
                .ToListAsync();

            if (currentCompareProducts.Any())
            {
                var existingGroups = currentCompareProducts
                    .Select(BuildCompareGroupInfo)
                    .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .ToList();

                if (existingGroups.Count > 1)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Danh sach compare hien dang chua nhieu nhom san pham cu. Vui long xoa bot de so sanh lai cung mot nhom."
                    });
                }

                var currentGroup = existingGroups[0];
                var targetGroup = BuildCompareGroupInfo(targetProduct);

                if (!string.Equals(currentGroup.Key, targetGroup.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Danh sach compare hien tai dang la {currentGroup.Label}. Ban chi nen them san pham cung nhom nhu {targetGroup.Label}."
                    });
                }
            }

            int currentCompareCount = await _dataContext.Compares
                .CountAsync(x => x.UserId == user.Id);

            if (currentCompareCount >= MaxCompareItems)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Bạn chỉ có thể so sánh tối đa {MaxCompareItems} sản phẩm."
                });
            }

            var compare = new CompareModel
            {
                ProductId = productId,
                UserId = user.Id
            };

            _dataContext.Compares.Add(compare);
            await _dataContext.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Đã thêm sản phẩm vào compare."
            });
        }

        [Authorize(Policy = PolicyNames.WishlistCompareAccess)]
        public async Task<IActionResult> Compare()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var compareEntries = await _dataContext.Compares
                .AsNoTracking()
                .AsSplitQuery()
                .Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.CreatedAt)
                .Include(x => x.Product)
                    .ThenInclude(p => p.Category)
                .Include(x => x.Product)
                    .ThenInclude(p => p.Publisher)
                .Include(x => x.Product)
                    .ThenInclude(p => p.ProductImages)
                .Include(x => x.Product)
                    .ThenInclude(p => p.RatingModel)
                .Include(x => x.Product)
                    .ThenInclude(p => p.Specifications)
                        .ThenInclude(s => s.SpecificationDefinition)
                .ToListAsync();

            var availableMap = await GetAvailableQuantityMapAsync(compareEntries.Select(x => x.ProductId));

            var items = compareEntries.Select(x =>
            {
                var ratings = x.Product.RatingModel ?? new List<RatingModel>();

                return new CompareItemVM
                {
                    Compare = x,
                    Product = x.Product,
                    AverageRating = ratings.Any()
                        ? Math.Round((decimal)ratings.Average(r => r.Stars), 1, MidpointRounding.AwayFromZero)
                        : 0,
                    ReviewCount = ratings.Count,
                    AvailableQuantity = availableMap.TryGetValue(x.ProductId, out var quantity) ? quantity : 0,
                    SummarySpecs = BuildSummarySpecs(x.Product),
                    SpecsByName = BuildSpecDictionary(x.Product),
                    SpecsByCode = BuildSpecCodeDictionary(x.Product),
                    NumericSpecsByCode = BuildNumericSpecDictionary(x.Product)
                };
            }).ToList();

            var sharedComponentType = GetSharedComponentType(items);
            var model = new ComparePageVM
            {
                Items = items,
                Sections = BuildCompareSections(items, sharedComponentType),
                ModeTitle = sharedComponentType.HasValue
                    ? $"So sánh {GetComponentTypeLabel(sharedComponentType.Value)}"
                    : "So sánh sản phẩm",
                ModeDescription = sharedComponentType.HasValue
                    ? GetComponentCompareDescription(sharedComponentType.Value)
                    : "Bảng compare đang giữ đầy đủ toàn bộ thông số và tự highlight các chỉ số quan trọng như giá, rating, tồn kho và các mốc số học dễ ra quyết định.",
                HasMixedComponentTypes = HasMixedComponentTypes(items)
            };

            return View(model);
        }

        [Authorize(Policy = PolicyNames.WishlistCompareAccess)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCompare(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var compare = await _dataContext.Compares
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);

            if (compare == null)
                return NotFound();

            _dataContext.Compares.Remove(compare);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Đã xóa sản phẩm khỏi compare.";
            return RedirectToAction(nameof(Compare));
        }

        [Authorize(Policy = PolicyNames.WishlistCompareAccess)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWishlist(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var wishlist = await _dataContext.Wishlists
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);

            if (wishlist == null)
                return NotFound();

            _dataContext.Wishlists.Remove(wishlist);
            await _dataContext.SaveChangesAsync();

            TempData["success"] = "Đã xóa sản phẩm khỏi wishlist.";
            return RedirectToAction(nameof(Wishlist));
        }

        [Authorize(Policy = PolicyNames.WishlistCompareAccess)]
        public async Task<IActionResult> Wishlist()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var wishlistEntries = await _dataContext.Wishlists
                .AsNoTracking()
                .AsSplitQuery()
                .Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.CreatedAt)
                .Include(x => x.Product)
                    .ThenInclude(p => p.Category)
                .Include(x => x.Product)
                    .ThenInclude(p => p.Publisher)
                .Include(x => x.Product)
                    .ThenInclude(p => p.ProductImages)
                .Include(x => x.Product)
                    .ThenInclude(p => p.RatingModel)
                .Include(x => x.Product)
                    .ThenInclude(p => p.Specifications)
                        .ThenInclude(s => s.SpecificationDefinition)
                .ToListAsync();

            var availableMap = await GetAvailableQuantityMapAsync(wishlistEntries.Select(x => x.ProductId));

            var data = wishlistEntries.Select(x =>
            {
                var ratings = x.Product.RatingModel ?? new List<RatingModel>();

                return new WishlistItemVM
                {
                    Wishlist = x,
                    Product = x.Product,
                    AverageRating = ratings.Any()
                        ? Math.Round((decimal)ratings.Average(r => r.Stars), 1, MidpointRounding.AwayFromZero)
                        : 0,
                    ReviewCount = ratings.Count,
                    AvailableQuantity = availableMap.TryGetValue(x.ProductId, out var quantity) ? quantity : 0,
                    SummarySpecs = BuildSummarySpecs(x.Product)
                };
            }).ToList();

            return View(data);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int statuscode)
        {
            if (statuscode == 404)
            {
                return View("NotFound");
            }

            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        public async Task<IActionResult> Contact()
        {
            if (!_memoryCache.TryGetValue(CacheKeys.ContactInfo, out ContactModel? contact))
            {
                _logger.LogInformation("CACHE MISS: ContactInfo");

                contact = await _dataContext.Contact
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                var contactCacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(10));

                _memoryCache.Set(CacheKeys.ContactInfo, contact, contactCacheOptions);
            }
            else
            {
                _logger.LogInformation("CACHE HIT: ContactInfo");
            }

            return View(contact);
        }

        private async Task<Dictionary<int, int>> GetAvailableQuantityMapAsync(IEnumerable<int> productIds)
        {
            var ids = productIds.Distinct().ToList();
            if (!ids.Any())
                return new Dictionary<int, int>();

            return await _dataContext.InventoryStocks
                .Where(x => ids.Contains(x.ProductId) && x.Warehouse.IsActive)
                .GroupBy(x => x.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AvailableQuantity = g.Sum(x => x.OnHandQuantity - x.ReservedQuantity)
                })
                .ToDictionaryAsync(x => x.ProductId, x => x.AvailableQuantity);
        }

        private static List<string> BuildSummarySpecs(ProductModel product)
        {
            return (product.Specifications ?? new List<ProductSpecificationModel>())
                .Where(x => x.SpecificationDefinition != null)
                .OrderBy(x => x.SpecificationDefinition.SortOrder)
                .ThenBy(x => x.SpecificationDefinition.Name)
                .Select(x => $"{x.SpecificationDefinition.Name}: {FormatSpecificationValue(x)}")
                .Where(x => !x.EndsWith(": ", StringComparison.Ordinal))
                .Take(4)
                .ToList();
        }

        private static Dictionary<string, string> BuildSpecDictionary(ProductModel product)
        {
            return (product.Specifications ?? new List<ProductSpecificationModel>())
                .Where(x => x.SpecificationDefinition != null)
                .OrderBy(x => x.SpecificationDefinition.SortOrder)
                .ThenBy(x => x.SpecificationDefinition.Name)
                .Select(x => new
                {
                    x.SpecificationDefinition.Name,
                    Value = FormatSpecificationValue(x)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .GroupBy(x => x.Name)
                .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> BuildSpecCodeDictionary(ProductModel product)
        {
            return (product.Specifications ?? new List<ProductSpecificationModel>())
                .Where(x => x.SpecificationDefinition != null)
                .OrderBy(x => x.SpecificationDefinition.SortOrder)
                .ThenBy(x => x.SpecificationDefinition.Name)
                .Select(x => new
                {
                    x.SpecificationDefinition.Code,
                    Value = FormatSpecificationValue(x)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Code) && !string.IsNullOrWhiteSpace(x.Value))
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, decimal> BuildNumericSpecDictionary(ProductModel product)
        {
            return (product.Specifications ?? new List<ProductSpecificationModel>())
                .Where(x => x.SpecificationDefinition != null && x.ValueNumber.HasValue)
                .OrderBy(x => x.SpecificationDefinition.SortOrder)
                .ThenBy(x => x.SpecificationDefinition.Name)
                .GroupBy(x => x.SpecificationDefinition.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().ValueNumber!.Value, StringComparer.OrdinalIgnoreCase);
        }

        private static List<CompareSectionVM> BuildCompareSections(List<CompareItemVM> items, PcComponentType? sharedComponentType)
        {
            if (!items.Any())
            {
                return new List<CompareSectionVM>();
            }

            var sections = new List<CompareSectionVM>
            {
                BuildOverviewSection(items)
            };

            var smartSection = BuildSmartCompareSection(items, sharedComponentType);
            if (smartSection != null && smartSection.Rows.Any())
            {
                sections.Add(smartSection);
            }

            var specSection = BuildAllSpecsSection(items);
            if (specSection.Rows.Any())
            {
                sections.Add(specSection);
            }

            return sections;
        }

        private static CompareSectionVM BuildOverviewSection(List<CompareItemVM> items)
        {
            return CreateSection(
                "Tổng quan nhanh",
                "Những chỉ số nền để nhìn ra sản phẩm nào dễ mua hơn, được đánh giá tốt hơn và còn hàng tốt hơn.",
                new[]
                {
                    BuildNumericRow("Giá bán", "Mức thấp hơn sẽ tiết kiệm ngân sách hơn.", "low", items,
                        item => item.Product.Price,
                        value => value.ToString("#,##0 đ", CultureInfo.InvariantCulture)),
                    BuildNumericRow("Rating trung bình", "Cao hơn thường cho thấy mức độ hài lòng tốt hơn.", "high", items,
                        item => item.AverageRating,
                        value => $"{value:0.0}/5"),
                    BuildNumericRow("Số lượt đánh giá", "Nhiều review hơn giúp kết quả rating đáng tin hơn.", "high", items,
                        item => (decimal)item.ReviewCount,
                        value => value.ToString("0", CultureInfo.InvariantCulture)),
                    BuildNumericRow("Tồn kho khả dụng", "Sản phẩm còn nhiều hàng sẽ dễ chốt đơn ngay hơn.", "high", items,
                        item => (decimal)item.AvailableQuantity,
                        value => value.ToString("0", CultureInfo.InvariantCulture))
                });
        }

        private static CompareSectionVM? BuildSmartCompareSection(List<CompareItemVM> items, PcComponentType? sharedComponentType)
        {
            if (!sharedComponentType.HasValue)
            {
                return null;
            }

            return sharedComponentType.Value switch
            {
                PcComponentType.CPU => BuildCpuSection(items),
                PcComponentType.Mainboard => BuildMainboardSection(items),
                PcComponentType.RAM => BuildRamSection(items),
                PcComponentType.SSD => BuildSsdSection(items),
                PcComponentType.GPU => BuildGpuSection(items),
                PcComponentType.PSU => BuildPsuSection(items),
                PcComponentType.Case => BuildCaseSection(items),
                PcComponentType.Cooler => BuildCoolerSection(items),
                PcComponentType.Monitor => BuildMonitorSection(items),
                _ => null
            };
        }

        private static CompareSectionVM BuildCpuSection(List<CompareItemVM> items)
        {
            return CreateSection(
                "Hiệu năng CPU",
                "Ưu tiên đa nhiệm dựa trên số nhân, số luồng, điện năng và tỷ lệ hiệu năng trên giá.",
                new CompareRowVM?[]
                {
                    BuildComputedRow("Điểm hiệu năng CPU", "Ưu tiên benchmark thực tế nếu đã nhập, nếu chưa có sẽ ước lượng từ số nhân và số luồng.", "high", items, item =>
                    {
                        var score = GetCpuComparableScore(item);
                        return score.HasValue
                            ? (score, $"{score:0} điểm", true)
                            : (null, "-", false);
                    }),
                    BuildComputedRow("Hiệu năng / giá", "Cao hơn sẽ đáng tiền hơn cho cùng tầm ngân sách.", "high", items, item =>
                    {
                        var score = GetCpuComparableScore(item);
                        if (!score.HasValue || item.Product.Price <= 0)
                        {
                            return (null, "-", false);
                        }

                        var ratio = score.Value / (item.Product.Price / 1_000_000m);
                        return (ratio, $"{ratio:0.0} điểm/1tr", true);
                    }),
                    BuildSpecNumericRow("Số nhân", "Cao hơn thường tốt hơn khi xử lý đa nhiệm.", "high", items, "cpu_cores"),
                    BuildSpecNumericRow("Số luồng", "Cao hơn sẽ có lợi cho render và workload song song.", "high", items, "cpu_threads"),
                    BuildSpecTextRow("Socket CPU", "Kiểm tra để chọn mainboard tương thích.", items, "cpu_socket"),
                    BuildSpecNumericRow("TDP", "Thấp hơn sẽ mát và nhẹ điện hơn.", "low", items, "cpu_tdp_w")
                });
        }

        private static CompareSectionVM BuildMainboardSection(List<CompareItemVM> items)
        {
            return CreateSection(
                "Khả năng nâng cấp mainboard",
                "Tập trung vào socket, chipset và khả năng mở rộng RAM cho các build dài hạn.",
                new CompareRowVM?[]
                {
                    BuildComputedRow("Điểm nâng cấp ước lượng", "Ưu tiên main có nhiều khe RAM và hỗ trợ dung lượng lớn hơn.", "high", items, item =>
                    {
                        var slots = GetNumericSpec(item, "mb_ram_slots");
                        var maxRam = GetNumericSpec(item, "mb_max_ram_gb");
                        if (!slots.HasValue && !maxRam.HasValue)
                        {
                            return (null, "-", false);
                        }

                        var score = (slots ?? 0m) * 25m + (maxRam ?? 0m);
                        return (score, $"{score:0} điểm", true);
                    }),
                    BuildSpecTextRow("Socket Mainboard", "Phải khớp socket CPU.", items, "mb_socket"),
                    BuildSpecTextRow("Chipset", "Chipset càng mạnh càng có nhiều tính năng hơn.", items, "mb_chipset"),
                    BuildSpecNumericRow("Số khe RAM", "Nhiều khe hơn sẽ linh hoạt nâng cấp hơn.", "high", items, "mb_ram_slots"),
                    BuildSpecNumericRow("RAM tối đa", "Mức cao hơn sẽ phù hợp build cần nhiều bộ nhớ.", "high", items, "mb_max_ram_gb"),
                    BuildSpecTextRow("Loại RAM hỗ trợ", "Đối chiếu với RAM bạn đang muốn dùng.", items, "mb_ram_type"),
                    BuildSpecTextRow("Kích thước Mainboard", "ATX, M-ATX hay ITX ảnh hưởng tới case tương thích.", items, "mb_form_factor")
                });
        }

        private static CompareSectionVM BuildRamSection(List<CompareItemVM> items)
        {
            return CreateSection(
                "Hiệu quả bộ nhớ",
                "So trực tiếp dung lượng, bus và giá trên mỗi GB để nhìn ra kit RAM cân bằng nhất.",
                new CompareRowVM?[]
                {
                    BuildComputedRow("Băng thông ước lượng", "Dung lượng và bus càng cao sẽ có lợi cho đa nhiệm nặng.", "high", items, item =>
                    {
                        var capacity = GetNumericSpec(item, "ram_capacity_gb");
                        var bus = GetNumericSpec(item, "ram_bus_mhz");
                        if (!capacity.HasValue && !bus.HasValue)
                        {
                            return (null, "-", false);
                        }

                        var score = (capacity ?? 0m) * Math.Max(bus ?? 0m, 1m);
                        return (score, $"{score:0} điểm", true);
                    }),
                    BuildComputedRow("Giá / GB", "Thấp hơn sẽ kinh tế hơn cho cùng dung lượng.", "low", items, item =>
                    {
                        var capacity = GetNumericSpec(item, "ram_capacity_gb");
                        if (!capacity.HasValue || capacity.Value <= 0 || item.Product.Price <= 0)
                        {
                            return (null, "-", false);
                        }

                        var pricePerGb = item.Product.Price / capacity.Value;
                        return (pricePerGb, $"{pricePerGb:#,##0} đ/GB", true);
                    }),
                    BuildSpecTextRow("Loại RAM", "DDR4, DDR5... cần khớp mainboard.", items, "ram_type"),
                    BuildSpecNumericRow("Dung lượng RAM", "Cao hơn sẽ tốt hơn cho đa nhiệm.", "high", items, "ram_capacity_gb"),
                    BuildSpecNumericRow("Bus RAM", "Bus cao hơn thường đem lại băng thông tốt hơn.", "high", items, "ram_bus_mhz"),
                    BuildSpecNumericRow("Số thanh / kit", "Xem số module để lắp đúng khe và giữ dual-channel.", "neutral", items, "ram_kit_modules")
                });
        }

        private static CompareSectionVM BuildSsdSection(List<CompareItemVM> items)
        {
            return CreateSection(
                "Hiệu quả lưu trữ",
                "Ưu tiên dung lượng, chuẩn giao tiếp và chi phí trên mỗi GB để dễ chọn SSD hợp lý.",
                new CompareRowVM?[]
                {
                    BuildComputedRow("Điểm giao tiếp ước lượng", "Chuẩn NVMe/PCIe cao hơn sẽ có lợi cho tốc độ đọc ghi.", "high", items, item =>
                    {
                        var score = GetStorageInterfaceScore(GetTextSpec(item, "ssd_interface"));
                        return score.HasValue
                            ? (score, $"{score:0} điểm", true)
                            : (null, "-", false);
                    }),
                    BuildComputedRow("Giá / GB", "Thấp hơn sẽ tối ưu chi phí lưu trữ hơn.", "low", items, item =>
                    {
                        var capacity = GetNumericSpec(item, "ssd_capacity_gb");
                        if (!capacity.HasValue || capacity.Value <= 0 || item.Product.Price <= 0)
                        {
                            return (null, "-", false);
                        }

                        var pricePerGb = item.Product.Price / capacity.Value;
                        return (pricePerGb, $"{pricePerGb:#,##0} đ/GB", true);
                    }),
                    BuildSpecTextRow("Loại ổ", "SATA 2.5 inch hay M.2 sẽ ảnh hưởng khả năng lắp đặt.", items, "ssd_storage_type"),
                    BuildSpecNumericRow("Dung lượng SSD", "Cao hơn sẽ có nhiều không gian hơn.", "high", items, "ssd_capacity_gb"),
                    BuildSpecTextRow("Chuẩn giao tiếp", "PCIe Gen càng cao thường càng nhanh hơn.", items, "ssd_interface")
                });
        }

        private static CompareSectionVM BuildGpuSection(List<CompareItemVM> items)
        {
            return CreateSection(
                "Nhu cầu đồ họa",
                "Phần này nhấn mạnh benchmark, VRAM và điện năng để bạn cân giữa hiệu năng gaming và yêu cầu nguồn.",
                new CompareRowVM?[]
                {
                    BuildComputedRow("Điểm benchmark GPU", "Ưu tiên benchmark thực tế nếu đã nhập trong thông số kỹ thuật.", "high", items, item =>
                    {
                        var score = GetGpuComparableScore(item);
                        return score.HasValue
                            ? (score, $"{score:0} điểm", true)
                            : (null, "-", false);
                    }),
                    BuildComputedRow("Hiệu năng / giá", "Cao hơn sẽ đáng tiền hơn trong cùng tầm ngân sách.", "high", items, item =>
                    {
                        var score = GetGpuComparableScore(item);
                        if (!score.HasValue || item.Product.Price <= 0)
                        {
                            return (null, "-", false);
                        }

                        var ratio = score.Value / (item.Product.Price / 1_000_000m);
                        return (ratio, $"{ratio:0.0} điểm/1tr", true);
                    }),
                    BuildSpecTextRow("GPU", "Tên chip giúp nhận diện dòng card.", items, "gpu_chip"),
                    BuildSpecNumericRow("VRAM", "Cao hơn sẽ có lợi cho game nặng và độ phân giải cao.", "high", items, "gpu_vram_gb"),
                    BuildSpecNumericRow("Công suất VGA", "Thấp hơn sẽ nhẹ điện và mát hơn.", "low", items, "gpu_tdp_w"),
                    BuildSpecNumericRow("PSU đề nghị", "Mức thấp hơn sẽ dễ phối nguồn hơn.", "low", items, "gpu_recommended_psu_w"),
                    BuildSpecNumericRow("Chiều dài VGA", "Đối chiếu với case nếu không gian lắp đặt hạn chế.", "neutral", items, "gpu_length_mm")
                });
        }

        private static CompareSectionVM BuildPsuSection(List<CompareItemVM> items)
        {
            return CreateSection(
                "Công suất nguồn",
                "So tổng công suất, chuẩn hiệu suất và giá trên mỗi watt để chọn PSU đáng tiền hơn.",
                new CompareRowVM?[]
                {
                    BuildComputedRow("Giá / W", "Thấp hơn sẽ kinh tế hơn cho cùng công suất.", "low", items, item =>
                    {
                        var watt = GetNumericSpec(item, "psu_watt");
                        if (!watt.HasValue || watt.Value <= 0 || item.Product.Price <= 0)
                        {
                            return (null, "-", false);
                        }

                        var pricePerWatt = item.Product.Price / watt.Value;
                        return (pricePerWatt, $"{pricePerWatt:#,##0} đ/W", true);
                    }),
                    BuildComputedRow("Điểm hiệu suất ước lượng", "Chứng nhận càng cao thì hiệu suất điện càng tốt.", "high", items, item =>
                    {
                        var score = GetPsuEfficiencyScore(GetTextSpec(item, "psu_efficiency"));
                        return score.HasValue
                            ? (score, $"{score:0} điểm", true)
                            : (null, "-", false);
                    }),
                    BuildSpecNumericRow("Công suất PSU", "Cao hơn sẽ dư địa tốt hơn cho nâng cấp.", "high", items, "psu_watt"),
                    BuildSpecTextRow("Chứng nhận", "Ưu tiên Gold/Platinum nếu bạn cần độ ổn định và hiệu suất điện.", items, "psu_efficiency"),
                    BuildSpecTextRow("Chuẩn nguồn", "ATX, SFX... cần khớp với case.", items, "psu_standard")
                });
        }

        private static CompareSectionVM BuildCaseSection(List<CompareItemVM> items)
        {
            return CreateSection(
                "Không gian lắp đặt",
                "So sánh độ rộng build và số chuẩn hỗ trợ để dễ ghép linh kiện lớn hoặc cao cấp hơn.",
                new CompareRowVM?[]
                {
                    BuildComputedRow("Số chuẩn mainboard hỗ trợ", "Nhiều chuẩn hơn sẽ linh hoạt build hơn.", "high", items, item =>
                    {
                        var supported = GetTextSpec(item, "case_supported_mb_sizes");
                        if (string.IsNullOrWhiteSpace(supported))
                        {
                            return (null, "-", false);
                        }

                        var count = supported.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
                        return (count, $"{count} chuẩn", true);
                    }),
                    BuildSpecTextRow("Mainboard hỗ trợ", "Đối chiếu với form factor của mainboard.", items, "case_supported_mb_sizes"),
                    BuildSpecNumericRow("GPU dài tối đa", "Cao hơn sẽ dễ lắp card dài.", "high", items, "case_max_gpu_length_mm"),
                    BuildSpecNumericRow("Tản nhiệt cao tối đa", "Cao hơn sẽ linh hoạt với tower cooler.", "high", items, "case_max_cooler_height_mm"),
                    BuildSpecTextRow("Chuẩn PSU hỗ trợ", "Kiểm tra với nguồn ATX/SFX bạn đang nhắm đến.", items, "case_psu_standard")
                });
        }

        private static CompareSectionVM BuildCoolerSection(List<CompareItemVM> items)
        {
            return CreateSection(
                "Tương thích tản nhiệt",
                "Hiện tại dữ liệu cooler chủ yếu phục vụ so chiều cao để ghép với case phù hợp.",
                new CompareRowVM?[]
                {
                    BuildSpecNumericRow("Chiều cao tản nhiệt", "Thông số này dùng để đối chiếu với case, không phải càng cao càng tốt.", "neutral", items, "cooler_height_mm")
                });
        }

        private static CompareSectionVM BuildMonitorSection(List<CompareItemVM> items)
        {
            return CreateSection(
                "Trải nghiệm hiển thị",
                "Phân tích theo độ phân giải, tần số quét, độ sáng và một điểm hiển thị ước lượng cho nhu cầu gaming.",
                new CompareRowVM?[]
                {
                    BuildComputedRow("Điểm hiển thị ước lượng", "Ưu tiên màn có nhiều điểm ảnh và tần số quét cao hơn.", "high", items, item =>
                    {
                        var pixels = TryParseResolutionPixels(GetTextSpec(item, "monitor_resolution"));
                        var refresh = GetNumericSpec(item, "monitor_refresh_rate_hz");
                        if (!pixels.HasValue || !refresh.HasValue)
                        {
                            return (null, "-", false);
                        }

                        var score = (pixels.Value / 1_000_000m) * refresh.Value;
                        return (score, $"{score:0.##} điểm", true);
                    }),
                    BuildComputedRow("Độ phân giải thực", "Số điểm ảnh cao hơn sẽ nét hơn.", "high", items, item =>
                    {
                        var resolution = GetTextSpec(item, "monitor_resolution");
                        var pixels = TryParseResolutionPixels(resolution);
                        if (string.IsNullOrWhiteSpace(resolution))
                        {
                            return (null, "-", false);
                        }

                        return pixels.HasValue
                            ? (pixels, $"{resolution} ({pixels.Value / 1_000_000m:0.##} MP)", true)
                            : (null, resolution, false);
                    }),
                    BuildSpecNumericRow("Tần số quét", "Hz cao hơn sẽ mượt hơn cho gaming.", "high", items, "monitor_refresh_rate_hz"),
                    BuildSpecNumericRow("Độ sáng", "Cao hơn sẽ dễ nhìn hơn trong môi trường sáng.", "high", items, "monitor_brightness_nits"),
                    BuildSpecNumericRow("Kích thước màn hình", "Thông số tham khảo, không mặc định càng lớn càng tốt.", "neutral", items, "monitor_size_inch"),
                    BuildSpecTextRow("Tấm nền", "IPS, VA, OLED... ảnh hưởng màu sắc và độ tương phản.", items, "monitor_panel_type")
                });
        }

        private static CompareSectionVM BuildAllSpecsSection(List<CompareItemVM> items)
        {
            var definitions = items
                .SelectMany(item => item.Product.Specifications ?? new List<ProductSpecificationModel>())
                .Where(spec => spec.SpecificationDefinition != null)
                .Select(spec => spec.SpecificationDefinition)
                .GroupBy(def => def.Code, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(def => def.ComponentType ?? PcComponentType.None)
                .ThenBy(def => def.SortOrder)
                .ThenBy(def => def.Name)
                .ToList();

            var rows = definitions.Select(definition =>
            {
                var preference = GetSpecPreference(definition.Code);
                return BuildComputedRow(definition.Name, GetSpecHint(definition.Code), preference, items, item =>
                {
                    var display = GetTextSpec(item, definition.Code);
                    bool hasNumeric = item.NumericSpecsByCode.TryGetValue(definition.Code, out var numeric);

                    if (string.IsNullOrWhiteSpace(display))
                    {
                        return (null, "-", false);
                    }

                    return hasNumeric
                        ? (numeric, display, preference != "neutral")
                        : (null, display, false);
                });
            });

            return CreateSection(
                "Toàn bộ thông số",
                "Danh sách spec chi tiết của từng sản phẩm để bạn đối chiếu từng dòng kỹ thuật.",
                rows);
        }

        private static CompareSectionVM CreateSection(string title, string? description, IEnumerable<CompareRowVM?> rows)
        {
            return new CompareSectionVM
            {
                Title = title,
                Description = description,
                Rows = rows
                    .Where(row => row != null && HasMeaningfulData(row))
                    .Cast<CompareRowVM>()
                    .ToList()
            };
        }

        private static CompareRowVM BuildNumericRow(
            string label,
            string? hint,
            string preference,
            List<CompareItemVM> items,
            Func<CompareItemVM, decimal?> selector,
            Func<decimal, string> formatter)
        {
            return BuildComputedRow(label, hint, preference, items, item =>
            {
                var value = selector(item);
                return value.HasValue
                    ? (value, formatter(value.Value), true)
                    : (null, "-", false);
            });
        }

        private static CompareRowVM BuildSpecNumericRow(
            string label,
            string? hint,
            string preference,
            List<CompareItemVM> items,
            string code)
        {
            return BuildComputedRow(label, hint, preference, items, item =>
            {
                var display = GetTextSpec(item, code);
                bool hasNumeric = item.NumericSpecsByCode.TryGetValue(code, out var numeric);

                if (string.IsNullOrWhiteSpace(display))
                {
                    return (null, "-", false);
                }

                return hasNumeric
                    ? (numeric, display, preference != "neutral")
                    : (null, display, false);
            });
        }

        private static CompareRowVM BuildSpecTextRow(
            string label,
            string? hint,
            List<CompareItemVM> items,
            string code)
        {
            return BuildComputedRow(label, hint, "neutral", items, item =>
            {
                var display = GetTextSpec(item, code);
                return string.IsNullOrWhiteSpace(display)
                    ? (null, "-", false)
                    : (null, display, false);
            });
        }

        private static CompareRowVM BuildComputedRow(
            string label,
            string? hint,
            string preference,
            List<CompareItemVM> items,
            Func<CompareItemVM, (decimal? NumericValue, string DisplayValue, bool IsComparable)> selector)
        {
            var row = new CompareRowVM
            {
                Label = label,
                Hint = hint,
                Preference = preference,
                Cells = items.Select(item =>
                {
                    var cell = selector(item);
                    return new CompareCellVM
                    {
                        Value = string.IsNullOrWhiteSpace(cell.DisplayValue) ? "-" : cell.DisplayValue,
                        NumericValue = cell.NumericValue,
                        IsComparable = cell.IsComparable
                    };
                }).ToList()
            };

            MarkBestCells(row);
            return row;
        }

        private static bool HasMeaningfulData(CompareRowVM row)
        {
            return row.Cells.Any(cell => !string.Equals(cell.Value, "-", StringComparison.Ordinal));
        }

        private static void MarkBestCells(CompareRowVM row)
        {
            if (row.Preference != "high" && row.Preference != "low")
            {
                return;
            }

            var comparableCells = row.Cells
                .Where(cell => cell.IsComparable && cell.NumericValue.HasValue)
                .ToList();

            if (comparableCells.Count < 2)
            {
                return;
            }

            var values = comparableCells
                .Select(cell => cell.NumericValue!.Value)
                .Distinct()
                .ToList();

            if (values.Count < 2)
            {
                return;
            }

            var target = row.Preference == "low"
                ? comparableCells.Min(cell => cell.NumericValue!.Value)
                : comparableCells.Max(cell => cell.NumericValue!.Value);

            foreach (var cell in comparableCells)
            {
                if (Math.Abs(cell.NumericValue!.Value - target) <= 0.0001m)
                {
                    cell.IsBest = true;
                }
            }
        }

        private static decimal? GetCpuEstimatedScore(CompareItemVM item)
        {
            var cores = GetNumericSpec(item, "cpu_cores");
            var threads = GetNumericSpec(item, "cpu_threads");

            if (!cores.HasValue && !threads.HasValue)
            {
                return null;
            }

            return (cores ?? 0m) * 100m + (threads ?? 0m) * 55m;
        }

        private static decimal? GetCpuComparableScore(CompareItemVM item)
        {
            return GetFirstAvailableNumericSpec(item, "cpu_benchmark_score", "cpu_performance_score")
                ?? GetCpuEstimatedScore(item);
        }

        private static decimal? GetGpuComparableScore(CompareItemVM item)
        {
            return GetFirstAvailableNumericSpec(item, "gpu_benchmark_score", "gpu_performance_score");
        }

        private static decimal? GetFirstAvailableNumericSpec(CompareItemVM item, params string[] codes)
        {
            foreach (var code in codes)
            {
                var value = GetNumericSpec(item, code);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        private static decimal? GetNumericSpec(CompareItemVM item, string code)
        {
            return item.NumericSpecsByCode.TryGetValue(code, out var value) ? value : null;
        }

        private static string GetTextSpec(CompareItemVM item, string code)
        {
            return item.SpecsByCode.TryGetValue(code, out var value) ? value : string.Empty;
        }

        private static decimal? TryParseResolutionPixels(string? resolution)
        {
            if (string.IsNullOrWhiteSpace(resolution))
            {
                return null;
            }

            var match = Regex.Match(resolution, @"(\d{3,5})\s*[xX]\s*(\d{3,5})");
            if (match.Success &&
                decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var width) &&
                decimal.TryParse(match.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var height))
            {
                return width * height;
            }

            var normalized = resolution.Trim().ToLowerInvariant();

            if (normalized.Contains("8k"))
            {
                return 7680m * 4320m;
            }

            if (normalized.Contains("4k") || normalized.Contains("uhd"))
            {
                return 3840m * 2160m;
            }

            if (normalized.Contains("2k") || normalized.Contains("qhd") || normalized.Contains("1440p"))
            {
                return 2560m * 1440m;
            }

            if (normalized.Contains("fhd") || normalized.Contains("1080p"))
            {
                return 1920m * 1080m;
            }

            if (normalized.Contains("hd+") || normalized.Contains("900p"))
            {
                return 1600m * 900m;
            }

            if (normalized.Contains("hd") || normalized.Contains("720p"))
            {
                return 1280m * 720m;
            }

            return null;
        }

        private static decimal? GetStorageInterfaceScore(string? interfaceValue)
        {
            if (string.IsNullOrWhiteSpace(interfaceValue))
            {
                return null;
            }

            var normalized = interfaceValue.Trim().ToLowerInvariant();

            if (normalized.Contains("pcie 5") || normalized.Contains("gen5"))
            {
                return 500m;
            }

            if (normalized.Contains("pcie 4") || normalized.Contains("gen4"))
            {
                return 400m;
            }

            if (normalized.Contains("pcie 3") || normalized.Contains("gen3"))
            {
                return 300m;
            }

            if (normalized.Contains("nvme"))
            {
                return 250m;
            }

            if (normalized.Contains("sata iii") || normalized.Contains("sata 3"))
            {
                return 120m;
            }

            if (normalized.Contains("sata"))
            {
                return 100m;
            }

            return 50m;
        }

        private static decimal? GetPsuEfficiencyScore(string? efficiencyValue)
        {
            if (string.IsNullOrWhiteSpace(efficiencyValue))
            {
                return null;
            }

            var normalized = efficiencyValue.Trim().ToLowerInvariant();

            if (normalized.Contains("titanium"))
            {
                return 6m;
            }

            if (normalized.Contains("platinum"))
            {
                return 5m;
            }

            if (normalized.Contains("gold"))
            {
                return 4m;
            }

            if (normalized.Contains("silver"))
            {
                return 3m;
            }

            if (normalized.Contains("bronze"))
            {
                return 2m;
            }

            if (normalized.Contains("white") || normalized.Contains("standard"))
            {
                return 1m;
            }

            return null;
        }

        private static PcComponentType? GetSharedComponentType(List<CompareItemVM> items)
        {
            if (!items.Any())
            {
                return null;
            }

            var firstType = items[0].Product.ComponentType;
            if (!firstType.HasValue || firstType.Value == PcComponentType.None)
            {
                return null;
            }

            return items.All(item => item.Product.ComponentType == firstType)
                ? firstType
                : null;
        }

        private static bool HasMixedComponentTypes(List<CompareItemVM> items)
        {
            return items
                .Select(item => item.Product.ComponentType ?? PcComponentType.None)
                .Distinct()
                .Count() > 1;
        }

        private static string GetComponentTypeLabel(PcComponentType componentType)
        {
            return componentType switch
            {
                PcComponentType.CPU => "CPU",
                PcComponentType.Mainboard => "Mainboard",
                PcComponentType.RAM => "RAM",
                PcComponentType.SSD => "SSD",
                PcComponentType.HDD => "HDD",
                PcComponentType.GPU => "GPU",
                PcComponentType.PSU => "PSU",
                PcComponentType.Case => "Case",
                PcComponentType.Cooler => "Tản nhiệt",
                PcComponentType.Monitor => "Màn hình",
                _ => "linh kiện"
            };
        }

        private static string GetComponentCompareDescription(PcComponentType componentType)
        {
            return componentType switch
            {
                PcComponentType.CPU => "Chế độ compare CPU tập trung vào đa nhiệm, hiệu năng trên giá và điện năng tiêu thụ.",
                PcComponentType.Mainboard => "Chế độ compare mainboard ưu tiên socket, chipset và khả năng nâng cấp RAM.",
                PcComponentType.RAM => "Chế độ compare RAM nhấn mạnh dung lượng, bus và chi phí trên mỗi GB.",
                PcComponentType.SSD => "Chế độ compare SSD ưu tiên dung lượng, chuẩn giao tiếp và hiệu quả chi phí lưu trữ.",
                PcComponentType.GPU => "Chế độ compare GPU tập trung vào VRAM, điện năng và mức nguồn đề nghị.",
                PcComponentType.PSU => "Chế độ compare PSU so trực tiếp công suất, chứng nhận và giá trên mỗi watt.",
                PcComponentType.Case => "Chế độ compare case giúp nhìn nhanh độ rộng build và không gian cho GPU, cooler, PSU.",
                PcComponentType.Cooler => "Chế độ compare cooler hiện thiên về tính tương thích với case và độ cao tản nhiệt.",
                PcComponentType.Monitor => "Chế độ compare màn hình nhìn vào độ phân giải, tần số quét và độ sáng thực tế.",
                _ => "Chế độ compare đang hiển thị đầy đủ thông số để bạn đối chiếu chi tiết."
            };
        }

        private static CompareGroupInfo BuildCompareGroupInfo(ProductModel product)
        {
            if (product.ComponentType.HasValue && product.ComponentType.Value != PcComponentType.None)
            {
                return new CompareGroupInfo(
                    $"component:{(int)product.ComponentType.Value}",
                    GetComponentTypeLabel(product.ComponentType.Value));
            }

            if (product.ProductType == ProductType.Monitor)
            {
                return new CompareGroupInfo("monitor", GetComponentTypeLabel(PcComponentType.Monitor));
            }

            if (product.ProductType == ProductType.PcPrebuilt)
            {
                return new CompareGroupInfo("pc_prebuilt", "PC prebuilt");
            }

            return new CompareGroupInfo(
                $"category:{product.CategoryId}",
                product.Category?.Name ?? "san pham cung danh muc");
        }

        private static string GetSpecPreference(string? code)
        {
            return code?.ToLowerInvariant() switch
            {
                "cpu_benchmark_score" => "high",
                "cpu_performance_score" => "high",
                "cpu_cores" => "high",
                "cpu_threads" => "high",
                "cpu_tdp_w" => "low",
                "mb_ram_slots" => "high",
                "mb_max_ram_gb" => "high",
                "ram_capacity_gb" => "high",
                "ram_bus_mhz" => "high",
                "ssd_capacity_gb" => "high",
                "gpu_benchmark_score" => "high",
                "gpu_performance_score" => "high",
                "gpu_vram_gb" => "high",
                "gpu_tdp_w" => "low",
                "gpu_recommended_psu_w" => "low",
                "psu_watt" => "high",
                "case_max_gpu_length_mm" => "high",
                "case_max_cooler_height_mm" => "high",
                "monitor_refresh_rate_hz" => "high",
                "monitor_brightness_nits" => "high",
                _ => "neutral"
            };
        }

        private static string? GetSpecHint(string? code)
        {
            return code?.ToLowerInvariant() switch
            {
                "cpu_benchmark_score" => "Điểm benchmark càng cao thì hiệu năng CPU càng dễ nổi bật hơn trong so sánh.",
                "cpu_performance_score" => "Điểm hiệu năng càng cao càng thuận tiện để so nhanh giữa các CPU.",
                "cpu_cores" => "Nhiều nhân hơn thường tốt hơn cho xử lý đa nhiệm.",
                "cpu_threads" => "Nhiều luồng hơn có lợi cho render và workload nặng.",
                "cpu_tdp_w" => "Mức thấp hơn sẽ mát và nhẹ điện hơn.",
                "ram_bus_mhz" => "Bus cao hơn thường cho băng thông RAM tốt hơn.",
                "ssd_interface" => "Chuẩn giao tiếp càng mới thì tiềm năng tốc độ càng cao.",
                "gpu_benchmark_score" => "Điểm benchmark cao hơn sẽ phản ánh lợi thế hiệu năng GPU trực tiếp hơn.",
                "gpu_performance_score" => "Điểm hiệu năng cao hơn giúp nhìn nhanh mức chênh lệch giữa các VGA.",
                "gpu_vram_gb" => "VRAM cao hơn sẽ hữu ích cho game nặng và độ phân giải cao.",
                "psu_efficiency" => "Chứng nhận cao hơn sẽ có hiệu suất điện tốt hơn.",
                "monitor_refresh_rate_hz" => "Tần số quét càng cao thì chuyển động càng mượt.",
                _ => null
            };
        }

        private static string FormatSpecificationValue(ProductSpecificationModel spec)
        {
            if (!string.IsNullOrWhiteSpace(spec.ValueText))
            {
                return spec.ValueText.Trim();
            }

            if (spec.ValueNumber.HasValue)
            {
                string formattedNumber = spec.ValueNumber.Value % 1 == 0
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
                    var values = JsonSerializer.Deserialize<List<string>>(spec.ValueJson) ?? new List<string>();
                    return string.Join(", ", values.Where(x => !string.IsNullOrWhiteSpace(x)));
                }
                catch
                {
                    return spec.ValueJson;
                }
            }

            return string.Empty;
        }
    }
}
