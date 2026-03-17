using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Eshop.Services
{
    public class PcCompatibilityService : IPcCompatibilityService
    {
        private readonly DataContext _context;

        public PcCompatibilityService(DataContext context)
        {
            _context = context;
        }

        public async Task<PcBuildCheckResponse> CheckAsync(PcBuildCheckRequest request)
        {
            var response = new PcBuildCheckResponse();

            if (request.Items == null || !request.Items.Any())
            {
                response.Messages.Add(new CompatibilityMessageDto
                {
                    Level = "info",
                    Message = "Chưa có linh kiện nào được chọn."
                });
                return response;
            }

            var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();

            var products = await _context.Products
                .Include(x => x.Specifications)
                    .ThenInclude(x => x.SpecificationDefinition)
                .Where(x => productIds.Contains(x.Id))
                .ToListAsync();

            var selected = request.Items
                .Join(products, r => r.ProductId, p => p.Id, (r, p) => new SelectedItem(r, p))
                .ToList();

            response.TotalPrice = selected.Sum(x => x.Product.Price * x.Request.Quantity);
            response.EstimatedPower = EstimatePower(selected);

            var cpu = selected.FirstOrDefault(x => x.Request.ComponentType == PcComponentType.CPU)?.Product;
            var mb = selected.FirstOrDefault(x => x.Request.ComponentType == PcComponentType.Mainboard)?.Product;
            var psu = selected.FirstOrDefault(x => x.Request.ComponentType == PcComponentType.PSU)?.Product;
            var pcCase = selected.FirstOrDefault(x => x.Request.ComponentType == PcComponentType.Case)?.Product;
            var cooler = selected.FirstOrDefault(x => x.Request.ComponentType == PcComponentType.Cooler)?.Product;

            var rams = selected.Where(x => x.Request.ComponentType == PcComponentType.RAM).ToList();
            var gpus = selected.Where(x => x.Request.ComponentType == PcComponentType.GPU).ToList();

            CheckCpuAndMainboard(cpu, mb, response);
            CheckRamAndMainboard(rams, mb, response);
            CheckMainboardAndCase(mb, pcCase, response);
            CheckGpuAndCase(gpus, pcCase, response);
            CheckCoolerAndCase(cooler, pcCase, response);
            CheckPsu(psu, response.EstimatedPower, gpus, response);
            CheckPsuAndCase(psu, pcCase, response);

            response.IsValid = !response.Messages.Any(x => x.Level == "error");
            return response;
        }

        private sealed record SelectedItem(PcBuildCheckItemDto Request, ProductModel Product);

        private string? GetText(ProductModel product, string code)
        {
            return product.Specifications
                .FirstOrDefault(x => x.SpecificationDefinition.Code == code)
                ?.ValueText;
        }

        private decimal? GetNumber(ProductModel product, string code)
        {
            return product.Specifications
                .FirstOrDefault(x => x.SpecificationDefinition.Code == code)
                ?.ValueNumber;
        }

        private List<string> GetJsonList(ProductModel product, string code)
        {
            var raw = product.Specifications
                .FirstOrDefault(x => x.SpecificationDefinition.Code == code)
                ?.ValueJson;

            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void CheckCpuAndMainboard(ProductModel? cpu, ProductModel? mb, PcBuildCheckResponse response)
        {
            if (cpu == null || mb == null) return;

            var cpuSocket = GetText(cpu, "cpu_socket");
            var mbSocket = GetText(mb, "mb_socket");

            if (!string.IsNullOrWhiteSpace(cpuSocket) &&
                !string.IsNullOrWhiteSpace(mbSocket) &&
                !string.Equals(cpuSocket, mbSocket, StringComparison.OrdinalIgnoreCase))
            {
                response.Messages.Add(new CompatibilityMessageDto
                {
                    Level = "error",
                    Message = $"CPU socket {cpuSocket} không tương thích với mainboard socket {mbSocket}."
                });
            }
        }

        private void CheckRamAndMainboard(List<SelectedItem> rams, ProductModel? mb, PcBuildCheckResponse response)
        {
            if (mb == null || !rams.Any()) return;

            var mbRamType = GetText(mb, "mb_ram_type");
            var ramSlots = GetNumber(mb, "mb_ram_slots");

            var totalModules = 0;

            foreach (var item in rams)
            {
                var ramType = GetText(item.Product, "ram_type");
                var kitModules = (int)(GetNumber(item.Product, "ram_kit_modules") ?? 1);

                totalModules += kitModules * item.Request.Quantity;

                if (!string.IsNullOrWhiteSpace(mbRamType) &&
                    !string.IsNullOrWhiteSpace(ramType) &&
                    !string.Equals(mbRamType, ramType, StringComparison.OrdinalIgnoreCase))
                {
                    response.Messages.Add(new CompatibilityMessageDto
                    {
                        Level = "error",
                        Message = $"RAM {item.Product.Name} ({ramType}) không tương thích với mainboard hỗ trợ {mbRamType}."
                    });
                }
            }

            if (ramSlots.HasValue && totalModules > ramSlots.Value)
            {
                response.Messages.Add(new CompatibilityMessageDto
                {
                    Level = "error",
                    Message = $"Tổng số thanh RAM ({totalModules}) vượt quá số khe mainboard ({ramSlots})."
                });
            }
        }

        private void CheckMainboardAndCase(ProductModel? mb, ProductModel? pcCase, PcBuildCheckResponse response)
        {
            if (mb == null || pcCase == null) return;

            var formFactor = GetText(mb, "mb_form_factor");
            var supported = GetJsonList(pcCase, "case_supported_mb_sizes");

            if (!string.IsNullOrWhiteSpace(formFactor) &&
                supported.Any() &&
                !supported.Any(x => x.Equals(formFactor, StringComparison.OrdinalIgnoreCase)))
            {
                response.Messages.Add(new CompatibilityMessageDto
                {
                    Level = "error",
                    Message = $"Case không hỗ trợ mainboard kích thước {formFactor}."
                });
            }
        }

        private void CheckGpuAndCase(List<SelectedItem> gpus, ProductModel? pcCase, PcBuildCheckResponse response)
        {
            if (pcCase == null || !gpus.Any()) return;

            var maxGpuLength = GetNumber(pcCase, "case_max_gpu_length_mm");

            if (!maxGpuLength.HasValue) return;

            foreach (var gpu in gpus)
            {
                var gpuLength = GetNumber(gpu.Product, "gpu_length_mm");
                if (gpuLength.HasValue && gpuLength > maxGpuLength.Value)
                {
                    response.Messages.Add(new CompatibilityMessageDto
                    {
                        Level = "error",
                        Message = $"VGA {gpu.Product.Name} dài {gpuLength}mm vượt quá giới hạn case {maxGpuLength}mm."
                    });
                }
            }
        }

        private void CheckCoolerAndCase(ProductModel? cooler, ProductModel? pcCase, PcBuildCheckResponse response)
        {
            if (cooler == null || pcCase == null) return;

            var coolerHeight = GetNumber(cooler, "cooler_height_mm");
            var maxCoolerHeight = GetNumber(pcCase, "case_max_cooler_height_mm");

            if (coolerHeight.HasValue && maxCoolerHeight.HasValue && coolerHeight > maxCoolerHeight)
            {
                response.Messages.Add(new CompatibilityMessageDto
                {
                    Level = "error",
                    Message = $"Tản nhiệt cao {coolerHeight}mm vượt quá giới hạn case {maxCoolerHeight}mm."
                });
            }
        }

        private void CheckPsu(ProductModel? psu, int estimatedPower, List<SelectedItem> gpus, PcBuildCheckResponse response)
        {
            if (psu == null) return;

            var psuWatt = GetNumber(psu, "psu_watt");
            if (!psuWatt.HasValue) return;

            var recommendByGpu = gpus
                .Select(x => GetNumber(x.Product, "gpu_recommended_psu_w") ?? 0)
                .DefaultIfEmpty(0)
                .Max();

            var recommend = Math.Max((decimal)Math.Ceiling(estimatedPower * 1.25m), recommendByGpu);

            if (psuWatt < recommend)
            {
                response.Messages.Add(new CompatibilityMessageDto
                {
                    Level = "warning",
                    Message = $"Nguồn {psuWatt}W có thể hơi yếu. Nên dùng khoảng {recommend}W trở lên."
                });
            }
        }

        private void CheckPsuAndCase(ProductModel? psu, ProductModel? pcCase, PcBuildCheckResponse response)
        {
            if (psu == null || pcCase == null) return;

            var psuStandard = GetText(psu, "psu_standard");
            var casePsuStandard = GetText(pcCase, "case_psu_standard");

            if (!string.IsNullOrWhiteSpace(psuStandard) &&
                !string.IsNullOrWhiteSpace(casePsuStandard) &&
                !string.Equals(psuStandard, casePsuStandard, StringComparison.OrdinalIgnoreCase))
            {
                response.Messages.Add(new CompatibilityMessageDto
                {
                    Level = "warning",
                    Message = $"PSU chuẩn {psuStandard} có thể không khớp hoàn toàn với case yêu cầu {casePsuStandard}."
                });
            }
        }

        private int EstimatePower(List<SelectedItem> items)
        {
            decimal total = 0;

            foreach (var item in items)
            {
                decimal oneItem = 0;

                switch (item.Request.ComponentType)
                {
                    case PcComponentType.CPU:
                        oneItem = GetNumber(item.Product, "cpu_tdp_w") ?? 0;
                        break;
                    case PcComponentType.GPU:
                        oneItem = GetNumber(item.Product, "gpu_tdp_w") ?? 0;
                        break;
                    default:
                        oneItem = 10;
                        break;
                }

                total += oneItem * item.Request.Quantity;
            }

            if (total < 150) total = 150;
            return (int)Math.Ceiling(total);
        }
    }
}