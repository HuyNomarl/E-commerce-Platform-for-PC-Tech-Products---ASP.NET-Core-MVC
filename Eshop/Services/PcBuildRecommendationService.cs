using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Services
{
    public class PcBuildRecommendationService : IPcBuildRecommendationService
    {
        private readonly DataContext _context;

        public PcBuildRecommendationService(DataContext context)
        {
            _context = context;
        }

        public async Task<PcBuildCheckRequest> RecommendBuildAsync(BuildRequirementProfile profile)
        {
            var result = new PcBuildCheckRequest
            {
                Items = new List<PcBuildCheckItemDto>()
            };

            var cpu = await PickCpu(profile);
            var mainboard = cpu != null ? await PickMainboardForCpu(cpu) : null;
            var ram = mainboard != null ? await PickRamForMainboard(mainboard, profile) : null;
            var gpu = await PickGpu(profile);
            var psu = await PickPsu(profile, gpu);
            var ssd = await PickSsd(profile);
            var pcCase = await PickCase(profile);
            var monitor = await PickMonitor(profile);

            AddItem(result, cpu, PcComponentType.CPU);
            AddItem(result, mainboard, PcComponentType.Mainboard);
            AddItem(result, ram, PcComponentType.RAM, 1);
            AddItem(result, gpu, PcComponentType.GPU);
            AddItem(result, psu, PcComponentType.PSU);
            AddItem(result, ssd, PcComponentType.SSD);
            AddItem(result, pcCase, PcComponentType.Case);
            AddItem(result, monitor, PcComponentType.Monitor);

            return result;
        }

        private static void AddItem(PcBuildCheckRequest request, ProductModel? product, PcComponentType type, int quantity = 1)
        {
            if (product == null) return;

            request.Items.Add(new PcBuildCheckItemDto
            {
                ProductId = product.Id,
                Quantity = quantity,
                ComponentType = type
            });
        }

        private async Task<ProductModel?> PickCpu(BuildRequirementProfile profile)
        {
            var query = _context.Products
                .Where(x => x.ComponentType == PcComponentType.CPU);

            if ((profile.GameTitle ?? "").Contains("valorant", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Price <= (profile.BudgetMax ?? decimal.MaxValue) * 0.25m);
            }

            return await query.OrderByDescending(x => x.Price).FirstOrDefaultAsync();
        }

        private async Task<ProductModel?> PickMainboardForCpu(ProductModel cpu)
        {
            var cpuSocket = await GetSpecText(cpu.Id, "cpu_socket");
            if (string.IsNullOrWhiteSpace(cpuSocket)) return null;

            return await _context.Products
                .Where(x => x.ComponentType == PcComponentType.Mainboard)
                .Where(x => x.Specifications.Any(s =>
                    s.SpecificationDefinition.Code == "mb_socket" &&
                    s.ValueText == cpuSocket))
                .OrderBy(x => x.Price)
                .FirstOrDefaultAsync();
        }

        private async Task<ProductModel?> PickRamForMainboard(ProductModel mainboard, BuildRequirementProfile profile)
        {
            var ramType = await GetSpecText(mainboard.Id, "mb_ram_type");
            if (string.IsNullOrWhiteSpace(ramType)) return null;

            return await _context.Products
                .Where(x => x.ComponentType == PcComponentType.RAM)
                .Where(x => x.Specifications.Any(s =>
                    s.SpecificationDefinition.Code == "ram_type" &&
                    s.ValueText == ramType))
                .OrderByDescending(x => x.Price)
                .FirstOrDefaultAsync();
        }

        private async Task<ProductModel?> PickGpu(BuildRequirementProfile profile)
        {
            var query = _context.Products
                .Where(x => x.ComponentType == PcComponentType.GPU);

            if ((profile.GameTitle ?? "").Contains("valorant", StringComparison.OrdinalIgnoreCase))
            {
                query = query.OrderBy(x => x.Price);
            }
            else
            {
                query = query.OrderByDescending(x => x.Price);
            }

            return await query.FirstOrDefaultAsync();
        }

        private async Task<ProductModel?> PickPsu(BuildRequirementProfile profile, ProductModel? gpu)
        {
            decimal minWatt = 550;

            if (gpu != null)
            {
                var recommend = await GetSpecNumber(gpu.Id, "gpu_recommended_psu_w");
                if (recommend.HasValue) minWatt = Math.Max(minWatt, recommend.Value);
            }

            return await _context.Products
                .Where(x => x.ComponentType == PcComponentType.PSU)
                .Where(x => x.Specifications.Any(s =>
                    s.SpecificationDefinition.Code == "psu_watt" &&
                    s.ValueNumber != null &&
                    s.ValueNumber >= minWatt))
                .OrderBy(x => x.Price)
                .FirstOrDefaultAsync();
        }

        private async Task<ProductModel?> PickSsd(BuildRequirementProfile profile)
        {
            return await _context.Products
                .Where(x => x.ComponentType == PcComponentType.SSD)
                .OrderByDescending(x => x.Price)
                .FirstOrDefaultAsync();
        }

        private async Task<ProductModel?> PickCase(BuildRequirementProfile profile)
        {
            return await _context.Products
                .Where(x => x.ComponentType == PcComponentType.Case)
                .OrderBy(x => x.Price)
                .FirstOrDefaultAsync();
        }

        private async Task<ProductModel?> PickMonitor(BuildRequirementProfile profile)
        {
            if (!profile.NeedsMonitorHighRefresh) return null;

            return await _context.Products
                .Where(x => x.ComponentType == PcComponentType.Monitor)
                .Where(x => x.Specifications.Any(s =>
                    s.SpecificationDefinition.Code == "monitor_refresh_rate_hz" &&
                    s.ValueNumber != null &&
                    s.ValueNumber >= 144))
                .OrderBy(x => x.Price)
                .FirstOrDefaultAsync();
        }

        private async Task<string?> GetSpecText(int productId, string code)
        {
            return await _context.ProductSpecifications
                .Where(x => x.ProductId == productId && x.SpecificationDefinition.Code == code)
                .Select(x => x.ValueText)
                .FirstOrDefaultAsync();
        }

        private async Task<decimal?> GetSpecNumber(int productId, string code)
        {
            return await _context.ProductSpecifications
                .Where(x => x.ProductId == productId && x.SpecificationDefinition.Code == code)
                .Select(x => x.ValueNumber)
                .FirstOrDefaultAsync();
        }
    }
}
