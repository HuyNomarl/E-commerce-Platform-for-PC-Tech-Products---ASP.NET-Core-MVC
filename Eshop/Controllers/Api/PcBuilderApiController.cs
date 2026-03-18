using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Eshop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Eshop.Controllers.Api
{
    [Route("api/pc-builder")]
    [ApiController]
    public class PcBuilderApiController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly IPcCompatibilityService _compatibilityService;

        public PcBuilderApiController(DataContext context, IPcCompatibilityService compatibilityService)
        {
            _context = context;
            _compatibilityService = compatibilityService;
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
            var query = _context.Products
                .Include(x => x.Publisher)
                .Include(x => x.Specifications)
                    .ThenInclude(x => x.SpecificationDefinition)
                .Where(x =>
                    x.ComponentType == componentType &&
                    (x.ProductType == ProductType.Component || x.ProductType == ProductType.Monitor));

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(x => x.Name.Contains(keyword));

            if (publisherId.HasValue && publisherId > 0)
                query = query.Where(x => x.PublisherId == publisherId);

            if (minPrice.HasValue)
                query = query.Where(x => x.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(x => x.Price <= maxPrice.Value);

            var total = await query.CountAsync();

            var products = await query
                .OrderBy(x => x.Price)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = products.Select(x => new PcBuilderProductCardDto
            {
                Id = x.Id,
                Name = x.Name,
                Image = x.Image,
                Price = x.Price,
                PublisherName = x.Publisher?.Name,
                PublisherId = x.PublisherId,
                Stock = x.Quantity,
                ComponentType = x.ComponentType.ToString(),
                SummarySpecs = BuildSummarySpecs(x),
                FilterSpecs = BuildFilterSpecs(x)
            }).ToList();

            return Ok(new
            {
                total,
                page,
                pageSize,
                items
            });
        }

        [HttpPost("add-to-cart")]
        public async Task<IActionResult> AddToCart([FromBody] SavePcBuildRequest request)
        {
            if (request.Items == null || !request.Items.Any())
                return BadRequest(new { message = "Chưa có linh kiện nào trong cấu hình." });

            var checkResult = await _compatibilityService.CheckAsync(new PcBuildCheckRequest
            {
                Items = request.Items
            });

            var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();

            var products = await _context.Products
                .Where(x => productIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            foreach (var item in request.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                    return BadRequest(new { message = $"Không tìm thấy sản phẩm ID = {item.ProductId}" });

                if (product.Quantity < item.Quantity)
                    return BadRequest(new { message = $"Sản phẩm \"{product.Name}\" chỉ còn {product.Quantity} trong kho." });
            }

            var build = new PcBuildModel
            {
                BuildName = string.IsNullOrWhiteSpace(request.BuildName) ? "PC Build mới" : request.BuildName!,
                TotalPrice = checkResult.TotalPrice,
                UserId = User.Identity?.IsAuthenticated == true
                    ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                    : null
            };

            _context.PcBuilds.Add(build);
            await _context.SaveChangesAsync();

            foreach (var item in request.Items)
            {
                _context.PcBuildItems.Add(new PcBuildItemModel
                {
                    PcBuildId = build.Id,
                    ComponentType = item.ComponentType,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
            }

            await _context.SaveChangesAsync();

            var cart = HttpContext.Session.GetJson<List<CartItemModel>>("Cart") ?? new List<CartItemModel>();
            var buildGroupKey = Guid.NewGuid().ToString("N");

            foreach (var item in request.Items)
            {
                var product = products[item.ProductId];

                var cartItem = new CartItemModel(product)
                {
                    Quantity = item.Quantity,
                    PcBuildId = build.Id,
                    BuildGroupKey = buildGroupKey,
                    BuildName = build.BuildName,
                    IsPcBuildItem = true,
                    ComponentType = item.ComponentType.ToString()
                };

                cart.Add(cartItem);
            }

            HttpContext.Session.SetJson("Cart", cart);

            return Ok(new
            {
                message = "Đã thêm cấu hình vào giỏ hàng.",
                buildId = build.Id,
                buildCode = build.BuildCode
            });
        }

        [HttpPost("check")]
        public async Task<IActionResult> Check([FromBody] PcBuildCheckRequest request)
        {
            if (request?.Items == null || !request.Items.Any())
            {
                return Ok(new
                {
                    totalPrice = 0,
                    estimatedPower = 0,
                    messages = new[]
                    {
                new { level = "info", message = "Chưa có linh kiện để kiểm tra tương thích." }
            }
                });
            }

            var result = await _compatibilityService.CheckAsync(request);
            return Ok(result);
        }

        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] SavePcBuildRequest request)
        {
            if (request.Items == null || !request.Items.Any())
                return BadRequest(new { message = "Chưa có linh kiện nào để lưu." });

            var checkResult = await _compatibilityService.CheckAsync(new PcBuildCheckRequest
            {
                Items = request.Items
            });

            var build = new PcBuildModel
            {
                BuildName = string.IsNullOrWhiteSpace(request.BuildName) ? "Cấu hình mới" : request.BuildName!,
                TotalPrice = checkResult.TotalPrice,
                UserId = User.Identity?.IsAuthenticated == true
                    ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                    : null
            };

            _context.PcBuilds.Add(build);
            await _context.SaveChangesAsync();

            foreach (var item in request.Items)
            {
                _context.PcBuildItems.Add(new PcBuildItemModel
                {
                    PcBuildId = build.Id,
                    ComponentType = item.ComponentType,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Lưu cấu hình thành công.",
                buildId = build.Id,
                buildCode = build.BuildCode
            });
        }

        private static List<string> BuildSummarySpecs(ProductModel product)
        {
            var specs = product.Specifications.ToDictionary(
                x => x.SpecificationDefinition.Code,
                x => x);

            var result = new List<string>();

            switch (product.ComponentType)
            {
                case PcComponentType.CPU:
                    AddText(result, specs, "cpu_socket", "Socket");
                    AddNumber(result, specs, "cpu_cores", "Core");
                    AddNumber(result, specs, "cpu_threads", "Thread");
                    break;

                case PcComponentType.Mainboard:
                    AddText(result, specs, "mb_socket", "Socket");
                    AddText(result, specs, "mb_chipset", "Chipset");
                    AddText(result, specs, "mb_ram_type", "RAM");
                    break;

                case PcComponentType.RAM:
                    AddText(result, specs, "ram_type", "Loại");
                    AddNumber(result, specs, "ram_capacity_gb", "Dung lượng", "GB");
                    AddNumber(result, specs, "ram_bus_mhz", "Bus", "MHz");
                    break;

                case PcComponentType.GPU:
                    AddText(result, specs, "gpu_chip", "GPU");
                    AddNumber(result, specs, "gpu_vram_gb", "VRAM", "GB");
                    AddNumber(result, specs, "gpu_length_mm", "Dài", "mm");
                    break;

                case PcComponentType.PSU:
                    AddNumber(result, specs, "psu_watt", "Công suất", "W");
                    AddText(result, specs, "psu_efficiency", "Chuẩn");
                    break;

                case PcComponentType.Case:
                    AddNumber(result, specs, "case_max_gpu_length_mm", "GPU tối đa", "mm");
                    AddNumber(result, specs, "case_max_cooler_height_mm", "Tản tối đa", "mm");
                    break;

                case PcComponentType.SSD:
                    AddText(result, specs, "ssd_storage_type", "Loại");
                    AddNumber(result, specs, "ssd_capacity_gb", "Dung lượng", "GB");
                    AddText(result, specs, "ssd_interface", "Chuẩn");
                    break;

                case PcComponentType.Monitor:
                    AddNumber(result, specs, "monitor_size_inch", "Kích thước", "inch");
                    AddText(result, specs, "monitor_resolution", "Độ phân giải");
                    AddNumber(result, specs, "monitor_refresh_rate_hz", "Tần số", "Hz");
                    break;
            }

            return result;
        }

        private static void AddText(List<string> result, Dictionary<string, ProductSpecificationModel> specs, string code, string label)
        {
            if (specs.TryGetValue(code, out var spec) && !string.IsNullOrWhiteSpace(spec.ValueText))
            {
                result.Add($"{label}: {spec.ValueText}");
            }
        }

        private static void AddNumber(List<string> result, Dictionary<string, ProductSpecificationModel> specs, string code, string label, string? unit = null)
        {
            if (specs.TryGetValue(code, out var spec) && spec.ValueNumber.HasValue)
            {
                result.Add($"{label}: {spec.ValueNumber}{(string.IsNullOrWhiteSpace(unit) ? "" : $" {unit}")}");
            }
        }

        private static string FormatNumber(decimal value)
        {
            return value % 1 == 0 ? value.ToString("0") : value.ToString("0.##");
        }

        private static string? GetSpecText(ProductModel product, string code)
        {
            return product.Specifications
                .FirstOrDefault(x => x.SpecificationDefinition.Code == code)
                ?.ValueText;
        }

        private static decimal? GetSpecNumber(ProductModel product, string code)
        {
            return product.Specifications
                .FirstOrDefault(x => x.SpecificationDefinition.Code == code)
                ?.ValueNumber;
        }

        private static void AddTextFilterSpec(List<PcBuilderProductSpecDto> result, ProductModel product, string code, string label)
        {
            var value = GetSpecText(product, code);
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(new PcBuilderProductSpecDto
                {
                    Code = code,
                    Label = label,
                    Value = value.Trim()
                });
            }
        }

        private static void AddNumberFilterSpec(List<PcBuilderProductSpecDto> result, ProductModel product, string code, string label, string? unit = null)
        {
            var value = GetSpecNumber(product, code);
            if (value.HasValue)
            {
                result.Add(new PcBuilderProductSpecDto
                {
                    Code = code,
                    Label = label,
                    Value = $"{FormatNumber(value.Value)}{(string.IsNullOrWhiteSpace(unit) ? "" : $" {unit}")}"
                });
            }
        }

        private static List<PcBuilderProductSpecDto> BuildFilterSpecs(ProductModel product)
        {
            var result = new List<PcBuilderProductSpecDto>();

            switch (product.ComponentType)
            {
                case PcComponentType.CPU:
                    AddTextFilterSpec(result, product, "cpu_socket", "Socket CPU");
                    AddNumberFilterSpec(result, product, "cpu_tdp_w", "TDP", "W");
                    break;

                case PcComponentType.Mainboard:
                    AddTextFilterSpec(result, product, "mb_socket", "Socket Mainboard");
                    AddTextFilterSpec(result, product, "mb_chipset", "Chipset");
                    AddTextFilterSpec(result, product, "mb_form_factor", "Kích thước Mainboard");
                    AddTextFilterSpec(result, product, "mb_ram_type", "Loại RAM hỗ trợ");
                    AddNumberFilterSpec(result, product, "mb_ram_slots", "Số khe RAM");
                    break;

                case PcComponentType.RAM:
                    AddNumberFilterSpec(result, product, "ram_capacity_gb", "Dung lượng RAM", "GB");
                    AddTextFilterSpec(result, product, "ram_type", "Loại RAM");
                    AddNumberFilterSpec(result, product, "ram_bus_mhz", "Bus RAM", "MHz");
                    AddNumberFilterSpec(result, product, "ram_kit_modules", "Số thanh / kit");
                    break;

                case PcComponentType.SSD:
                    AddTextFilterSpec(result, product, "ssd_storage_type", "Loại ổ");
                    AddNumberFilterSpec(result, product, "ssd_capacity_gb", "Dung lượng SSD", "GB");
                    AddTextFilterSpec(result, product, "ssd_interface", "Chuẩn giao tiếp");
                    break;

                case PcComponentType.GPU:
                    AddTextFilterSpec(result, product, "gpu_chip", "GPU");
                    AddNumberFilterSpec(result, product, "gpu_vram_gb", "VRAM", "GB");
                    AddNumberFilterSpec(result, product, "gpu_length_mm", "Chiều dài VGA", "mm");
                    AddNumberFilterSpec(result, product, "gpu_recommended_psu_w", "PSU đề nghị", "W");
                    break;

                case PcComponentType.PSU:
                    AddNumberFilterSpec(result, product, "psu_watt", "Công suất PSU", "W");
                    AddTextFilterSpec(result, product, "psu_efficiency", "Chứng nhận");
                    AddTextFilterSpec(result, product, "psu_standard", "Chuẩn nguồn");
                    break;

                case PcComponentType.Case:
                    AddJsonArrayFilterSpec(result, product, "case_supported_mb_sizes", "Mainboard hỗ trợ");
                    AddNumberFilterSpec(result, product, "case_max_gpu_length_mm", "GPU dài tối đa", "mm");
                    AddNumberFilterSpec(result, product, "case_max_cooler_height_mm", "Tản nhiệt cao tối đa", "mm");
                    AddTextFilterSpec(result, product, "case_psu_standard", "Chuẩn PSU hỗ trợ");
                    break;

                case PcComponentType.Cooler:
                    AddNumberFilterSpec(result, product, "cooler_height_mm", "Chiều cao tản nhiệt", "mm");
                    break;

                case PcComponentType.Monitor:
                    AddNumberFilterSpec(result, product, "monitor_size_inch", "Kích thước màn hình", "inch");
                    AddTextFilterSpec(result, product, "monitor_resolution", "Độ phân giải");
                    AddNumberFilterSpec(result, product, "monitor_refresh_rate_hz", "Tần số quét", "Hz");
                    AddTextFilterSpec(result, product, "monitor_panel_type", "Tấm nền");
                    break;
            }

            return result;
        }
        private static string? GetSpecJson(ProductModel product, string code)
        {
            return product.Specifications
                .FirstOrDefault(x => x.SpecificationDefinition.Code == code)
                ?.ValueJson;
        }

        private static void AddJsonArrayFilterSpec(List<PcBuilderProductSpecDto> result, ProductModel product, string code, string label)
        {
            var raw = GetSpecJson(product, code);
            if (string.IsNullOrWhiteSpace(raw)) return;

            try
            {
                var values = JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
                foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    result.Add(new PcBuilderProductSpecDto
                    {
                        Code = code,
                        Label = label,
                        Value = value.Trim()
                    });
                }
            }
            catch
            {
            }


        }

    }
}