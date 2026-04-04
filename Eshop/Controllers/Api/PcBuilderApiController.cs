using Eshop.Constants;
using Eshop.Helpers;
using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Eshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Eshop.Controllers.Api
{
    [Route("api/pc-builder")]
    [ApiController]
    [IgnoreAntiforgeryToken]
    public class PcBuilderApiController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly IPcBuildChatService _pcBuildChatService;
        private readonly IInventoryService _inventoryService;
        private readonly ICartService _cartService;
        private readonly IPcBuildStorageService _pcBuildStorageService;
        private readonly IPcBuildWorkbookService _pcBuildWorkbookService;
        private readonly IPcBuildShareService _pcBuildShareService;

        public PcBuilderApiController(
            DataContext context,
            IPcBuildChatService pcBuildChatService,
            IInventoryService inventoryService,
            ICartService cartService,
            IPcBuildStorageService pcBuildStorageService,
            IPcBuildWorkbookService pcBuildWorkbookService,
            IPcBuildShareService pcBuildShareService)
        {
            _context = context;
            _pcBuildChatService = pcBuildChatService;
            _inventoryService = inventoryService;
            _cartService = cartService;
            _pcBuildStorageService = pcBuildStorageService;
            _pcBuildWorkbookService = pcBuildWorkbookService;
            _pcBuildShareService = pcBuildShareService;
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(
            PcComponentType componentType,
            string? keyword,
            int? publisherId,
            decimal? minPrice,
            decimal? maxPrice,
            int page = 1,
            int pageSize = 12)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 1000);

                var query = _context.Products
                    .AsNoTracking()
                    .Include(x => x.Publisher)
                    .Include(x => x.ProductImages)
                    .Include(x => x.Specifications)
                        .ThenInclude(x => x.SpecificationDefinition)
                    .Where(x =>
                        x.ComponentType == componentType &&
                        (x.ProductType == ProductType.Component || x.ProductType == ProductType.Monitor));

                query = query.WhereVisibleOnStorefront(_context);

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var normalizedKeyword = keyword.Trim();
                    query = query.Where(x => x.Name.Contains(normalizedKeyword));
                }

                if (publisherId.HasValue && publisherId > 0)
                {
                    query = query.Where(x => x.PublisherId == publisherId.Value);
                }

                if (minPrice.HasValue)
                {
                    query = query.Where(x => x.Price >= minPrice.Value);
                }

                if (maxPrice.HasValue)
                {
                    query = query.Where(x => x.Price <= maxPrice.Value);
                }

                var total = await query.CountAsync();
                var products = await query
                    .OrderBy(x => x.Price)
                    .ThenBy(x => x.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = products
                    .Select(PcBuilderProductMapper.ToCardDto)
                    .ToList();

                return Ok(new { total, page, pageSize, items });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = ex.Message,
                    detail = ex.InnerException?.Message
                });
            }
        }

        [HttpPost("check")]
        public async Task<IActionResult> Check([FromBody] PcBuildCheckRequest request)
        {
            var detail = await _pcBuildStorageService.BuildDetailAsync(null, request?.Items ?? new List<PcBuildCheckItemDto>());

            return Ok(new
            {
                totalPrice = detail.TotalPrice,
                estimatedPower = detail.EstimatedPower,
                isValid = detail.IsValid,
                messages = detail.Messages
            });
        }

        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] SavePcBuildRequest request)
        {
            if (request?.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "Chưa có linh kiện nào để lưu." });
            }

            try
            {
                var result = await _pcBuildStorageService.SaveAsync(
                    request.BuildName,
                    request.Items,
                    GetCurrentUserId(),
                    allowInvalidBuild: true);

                return Ok(new
                {
                    message = "Lưu cấu hình thành công.",
                    buildId = result.BuildId,
                    buildCode = result.BuildCode,
                    detail = result.Detail
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("add-to-cart")]
        public async Task<IActionResult> AddToCart([FromBody] SavePcBuildRequest request)
        {
            if (request?.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "Chưa có linh kiện nào trong cấu hình." });
            }

            try
            {
                await ReleaseActiveReservationIfAnyAsync("Giỏ hàng thay đổi từ PC Builder.");

                var detail = await _pcBuildStorageService.BuildDetailAsync(request.BuildName, request.Items);
                if (!detail.Items.Any())
                {
                    return BadRequest(new { message = "Chưa có linh kiện hợp lệ trong cấu hình." });
                }

                if (!detail.IsValid)
                {
                    var errorMessage = detail.Messages.FirstOrDefault(x => string.Equals(x.Level, "error", StringComparison.OrdinalIgnoreCase))
                        ?.Message;

                    return BadRequest(new
                    {
                        message = errorMessage ?? "Cấu hình đang có lỗi tương thích, chưa thể thêm vào giỏ hàng."
                    });
                }

                var cart = await _cartService.GetCartAsync(HttpContext);
                var requestedQtyByProduct = detail.Items
                    .GroupBy(x => x.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                var productIds = requestedQtyByProduct.Keys.ToList();
                var products = await _context.Products
                    .AsNoTracking()
                    .Include(x => x.ProductImages)
                    .WhereVisibleOnStorefront(_context)
                    .Where(x => productIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                foreach (var row in requestedQtyByProduct)
                {
                    if (!products.TryGetValue(row.Key, out var product))
                    {
                        return BadRequest(new { message = $"Không tìm thấy sản phẩm ID = {row.Key}" });
                    }

                    var availableStock = await _inventoryService.GetAvailableStockAsync(row.Key);
                    var quantityAlreadyInCart = cart
                        .Where(x => x.ProductId == row.Key)
                        .Sum(x => x.Quantity);

                    if (availableStock < quantityAlreadyInCart + row.Value)
                    {
                        return BadRequest(new
                        {
                            message = $"Sản phẩm \"{product.Name}\" chỉ còn {availableStock} trong kho, không đủ cho tổng số lượng hiện có trong giỏ và cấu hình mới."
                        });
                    }
                }

                var saved = await _pcBuildStorageService.SaveAsync(
                    detail.BuildName,
                    detail.Items.Select(x => new PcBuildCheckItemDto
                    {
                        ComponentType = x.ComponentType,
                        ProductId = x.ProductId,
                        Quantity = x.Quantity
                    }).ToList(),
                    GetCurrentUserId(),
                    allowInvalidBuild: false);

                var buildGroupKey = Guid.NewGuid().ToString("N");

                foreach (var item in saved.Detail.Items)
                {
                    if (!products.TryGetValue(item.ProductId, out var product))
                    {
                        continue;
                    }

                    cart.Add(new CartItemModel(product)
                    {
                        Quantity = item.Quantity,
                        PcBuildId = saved.BuildId,
                        BuildGroupKey = buildGroupKey,
                        BuildName = saved.Detail.BuildName,
                        IsPcBuildItem = true,
                        ComponentType = item.ComponentType.ToString()
                    });
                }

                await _cartService.SaveCartAsync(HttpContext, cart);

                return Ok(new
                {
                    message = "Đã thêm cấu hình vào giỏ hàng.",
                    buildId = saved.BuildId,
                    buildCode = saved.BuildCode
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("export-excel")]
        public async Task<IActionResult> ExportExcel([FromBody] SavePcBuildRequest request)
        {
            if (request?.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "Bạn chưa chọn linh kiện nào để xuất Excel." });
            }

            var detail = await _pcBuildStorageService.BuildDetailAsync(request.BuildName, request.Items);
            if (!detail.Items.Any())
            {
                return BadRequest(new { message = "Không có linh kiện hợp lệ để xuất Excel." });
            }

            var bytes = _pcBuildWorkbookService.Export(detail);
            var fileName = BuildFileName(detail.BuildName, ".xlsx");

            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost("import-excel")]
        public async Task<IActionResult> ImportExcel(IFormFile? file)
        {
            if (file == null || file.Length <= 0)
            {
                return BadRequest(new { message = "Bạn chưa chọn file Excel." });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var workbook = await _pcBuildWorkbookService.ImportAsync(stream, HttpContext.RequestAborted);
                var result = await _pcBuildStorageService.ResolveImportedRowsAsync(workbook.BuildName, workbook.Rows);
                result.SourceFileName = file.FileName;

                if (result.Errors.Any())
                {
                    return BadRequest(new
                    {
                        message = $"File Excel không hợp lệ. Có {result.Errors.Count} lỗi cần sửa.",
                        errors = result.Errors
                    });
                }

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Không thể đọc file Excel lúc này.",
                    detail = ex.Message
                });
            }
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpGet("share-users")]
        public async Task<IActionResult> SearchShareUsers(string? keyword)
        {
            var users = await _pcBuildShareService.SearchReceiversAsync(GetCurrentUserId()!, keyword);
            return Ok(users);
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpPost("share")]
        public async Task<IActionResult> Share([FromBody] PcBuildShareRequest request)
        {
            if (request?.Items == null || !request.Items.Any())
            {
                return BadRequest(new { message = "Bạn chưa chọn linh kiện nào để chia sẻ." });
            }

            try
            {
                var result = await _pcBuildShareService.ShareAsync(GetCurrentUserId()!, request);
                return Ok(new
                {
                    message = $"Đã chia sẻ cấu hình cho {result.ReceiverName}.",
                    shareCode = result.ShareCode,
                    buildCode = result.BuildCode,
                    buildName = result.BuildName
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpGet("shared")]
        public async Task<IActionResult> GetReceivedShares()
        {
            var items = await _pcBuildShareService.GetReceivedSharesAsync(GetCurrentUserId()!);
            return Ok(items);
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpGet("shared/{shareCode}")]
        public async Task<IActionResult> GetSharedBuild(string shareCode)
        {
            var detail = await _pcBuildShareService.GetSharedBuildAsync(GetCurrentUserId()!, shareCode);
            if (detail == null)
            {
                return NotFound(new { message = "Không tìm thấy cấu hình được chia sẻ." });
            }

            return Ok(detail);
        }

        [Authorize(Policy = PolicyNames.CustomerSelfService)]
        [HttpGet("shared/{shareCode}/excel")]
        public async Task<IActionResult> DownloadSharedBuildExcel(string shareCode)
        {
            var detail = await _pcBuildShareService.GetSharedBuildAsync(GetCurrentUserId()!, shareCode);
            if (detail == null)
            {
                return NotFound(new { message = "Không tìm thấy cấu hình được chia sẻ." });
            }

            var bytes = _pcBuildWorkbookService.Export(detail);
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                BuildFileName(detail.BuildName, ".xlsx"));
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] PcBuildChatRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new { message = "Bạn chưa nhập câu hỏi." });
                }

                var result = await _pcBuildChatService.AskAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = ex.Message,
                    detail = ex.InnerException?.Message
                });
            }
        }

        private string? GetCurrentUserId()
        {
            return User.Identity?.IsAuthenticated == true
                ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;
        }

        private static string BuildFileName(string buildName, string extension)
        {
            var safeName = string.IsNullOrWhiteSpace(buildName)
                ? "pc-build"
                : string.Concat(buildName.Trim().Select(ch =>
                    Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch));

            safeName = safeName.Replace(' ', '-');
            return $"{safeName}{extension}";
        }

        private async Task ReleaseActiveReservationIfAnyAsync(string reason)
        {
            var reservationCode = HttpContext.Session.GetString("ActiveReservationCode");

            if (string.IsNullOrWhiteSpace(reservationCode))
            {
                return;
            }

            await _inventoryService.ReleaseReservationAsync(
                reservationCode,
                User.Identity?.Name,
                reason);

            HttpContext.Session.Remove("ActiveReservationCode");
            HttpContext.Session.Remove("CheckoutInfo");
            HttpContext.Session.Remove("PendingCheckoutState");
        }
    }
}
