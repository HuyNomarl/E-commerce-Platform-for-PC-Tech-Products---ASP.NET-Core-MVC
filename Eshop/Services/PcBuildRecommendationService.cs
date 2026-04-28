using Eshop.Helpers;
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
            var plan = BuildRecommendationPlan.Create(profile);
            var result = new PcBuildCheckRequest
            {
                Items = new List<PcBuildCheckItemDto>()
            };

            var cpu = await PickCpuAsync(plan);
            var mainboard = cpu != null ? await PickMainboardForCpuAsync(cpu, plan) : null;
            var ram = mainboard != null ? await PickRamForMainboardAsync(mainboard, plan) : null;
            var gpu = plan.NeedsDedicatedGpu ? await PickGpuAsync(plan) : null;
            var psu = await PickPsuAsync(plan, gpu);
            var ssd = await PickSsdAsync(plan);
            var pcCase = await PickCaseAsync(plan, mainboard, gpu);
            var monitor = plan.IncludeMonitor ? await PickMonitorAsync(plan) : null;

            AddItem(result, cpu, PcComponentType.CPU);
            AddItem(result, mainboard, PcComponentType.Mainboard);
            AddItem(result, ram, PcComponentType.RAM, 1);
            AddItem(result, gpu, PcComponentType.GPU);
            AddItem(result, psu, PcComponentType.PSU);
            AddItem(result, ssd, PcComponentType.SSD);
            AddItem(result, pcCase, PcComponentType.Case);
            AddItem(result, monitor, PcComponentType.Monitor);

            if (plan.HasHardBudgetCap)
            {
                var totalPrice = result.Items.Sum(x => x.Quantity * GetSelectedPrice(
                    x.ComponentType,
                    cpu,
                    mainboard,
                    ram,
                    gpu,
                    psu,
                    ssd,
                    pcCase,
                    monitor));

                if (!HasCoreBuild(result.Items, plan) || totalPrice > plan.TotalBudget)
                {
                    return new PcBuildCheckRequest
                    {
                        Items = new List<PcBuildCheckItemDto>()
                    };
                }
            }

            return result;
        }

        private static void AddItem(PcBuildCheckRequest request, ProductModel? product, PcComponentType type, int quantity = 1)
        {
            if (product == null)
            {
                return;
            }

            request.Items.Add(new PcBuildCheckItemDto
            {
                ProductId = product.Id,
                Quantity = quantity,
                ComponentType = type
            });
        }

        private async Task<ProductModel?> PickCpuAsync(BuildRecommendationPlan plan)
        {
            var candidates = await LoadCandidatesAsync(
                GetVisibleComponentsQuery()
                    .Where(x => x.ComponentType == PcComponentType.CPU),
                plan,
                PcComponentType.CPU);

            if (!candidates.Any())
            {
                return null;
            }

            return candidates
                .OrderByDescending(x => ScoreCpuCandidate(x, plan))
                .ThenBy(x => x.Price)
                .FirstOrDefault();
        }

        private async Task<ProductModel?> PickMainboardForCpuAsync(ProductModel cpu, BuildRecommendationPlan plan)
        {
            var cpuSocket = GetText(cpu, "cpu_socket");
            if (string.IsNullOrWhiteSpace(cpuSocket))
            {
                return null;
            }

            var candidates = await LoadCandidatesAsync(
                GetVisibleComponentsQuery()
                    .Where(x => x.ComponentType == PcComponentType.Mainboard)
                    .Where(x => x.Specifications.Any(s =>
                        s.SpecificationDefinition.Code == "mb_socket" &&
                        s.ValueText == cpuSocket)),
                plan,
                PcComponentType.Mainboard);

            if (!candidates.Any())
            {
                return null;
            }

            return candidates
                .OrderByDescending(x => ScoreMainboardCandidate(x, plan))
                .ThenBy(x => x.Price)
                .FirstOrDefault();
        }

        private async Task<ProductModel?> PickRamForMainboardAsync(ProductModel mainboard, BuildRecommendationPlan plan)
        {
            var ramType = GetText(mainboard, "mb_ram_type");
            if (string.IsNullOrWhiteSpace(ramType))
            {
                return null;
            }

            var candidates = await LoadCandidatesAsync(
                GetVisibleComponentsQuery()
                    .Where(x => x.ComponentType == PcComponentType.RAM)
                    .Where(x => x.Specifications.Any(s =>
                        s.SpecificationDefinition.Code == "ram_type" &&
                        s.ValueText == ramType)),
                plan,
                PcComponentType.RAM);

            if (!candidates.Any())
            {
                return null;
            }

            var preferred = candidates
                .Where(x => (GetNumber(x, "ram_capacity_gb") ?? 0m) >= plan.TargetRamGb)
                .ToList();

            return (preferred.Any() ? preferred : candidates)
                .OrderByDescending(x => ScoreRamCandidate(x, plan))
                .ThenBy(x => x.Price)
                .FirstOrDefault();
        }

        private async Task<ProductModel?> PickGpuAsync(BuildRecommendationPlan plan)
        {
            var candidates = await LoadCandidatesAsync(
                GetVisibleComponentsQuery()
                    .Where(x => x.ComponentType == PcComponentType.GPU),
                plan,
                PcComponentType.GPU);

            if (!candidates.Any())
            {
                return null;
            }

            return candidates
                .OrderByDescending(x => ScoreGpuCandidate(x, plan))
                .ThenBy(x => x.Price)
                .FirstOrDefault();
        }

        private async Task<ProductModel?> PickPsuAsync(BuildRecommendationPlan plan, ProductModel? gpu)
        {
            var minimumPsuWatt = 450m;
            if (gpu != null)
            {
                minimumPsuWatt = Math.Max(minimumPsuWatt, GetNumber(gpu, "gpu_recommended_psu_w") ?? 450m);
            }

            if (plan.NeedsDedicatedGpu)
            {
                minimumPsuWatt = Math.Max(minimumPsuWatt, 650m);
            }

            var candidates = await LoadCandidatesAsync(
                GetVisibleComponentsQuery()
                    .Where(x => x.ComponentType == PcComponentType.PSU)
                    .Where(x => x.Specifications.Any(s =>
                        s.SpecificationDefinition.Code == "psu_watt" &&
                        s.ValueNumber != null &&
                        s.ValueNumber >= minimumPsuWatt)),
                plan,
                PcComponentType.PSU);

            if (!candidates.Any())
            {
                return null;
            }

            return candidates
                .OrderByDescending(x => ScorePsuCandidate(x, plan, minimumPsuWatt))
                .ThenBy(x => x.Price)
                .FirstOrDefault();
        }

        private async Task<ProductModel?> PickSsdAsync(BuildRecommendationPlan plan)
        {
            var candidates = await LoadCandidatesAsync(
                GetVisibleComponentsQuery()
                    .Where(x => x.ComponentType == PcComponentType.SSD),
                plan,
                PcComponentType.SSD);

            if (!candidates.Any())
            {
                return null;
            }

            var preferred = candidates
                .Where(x => (GetNumber(x, "ssd_capacity_gb") ?? 0m) >= plan.TargetSsdGb)
                .ToList();

            return (preferred.Any() ? preferred : candidates)
                .OrderByDescending(x => ScoreSsdCandidate(x, plan))
                .ThenBy(x => x.Price)
                .FirstOrDefault();
        }

        private async Task<ProductModel?> PickCaseAsync(
            BuildRecommendationPlan plan,
            ProductModel? mainboard,
            ProductModel? gpu)
        {
            var candidates = await LoadCandidatesAsync(
                GetVisibleComponentsQuery()
                    .Where(x => x.ComponentType == PcComponentType.Case),
                plan,
                PcComponentType.Case);

            if (!candidates.Any())
            {
                return null;
            }

            var filtered = candidates
                .Where(x => SupportsMainboard(x, mainboard) && SupportsGpuLength(x, gpu))
                .ToList();

            return (filtered.Any() ? filtered : candidates)
                .OrderByDescending(x => ScoreCaseCandidate(x, plan))
                .ThenBy(x => x.Price)
                .FirstOrDefault();
        }

        private async Task<ProductModel?> PickMonitorAsync(BuildRecommendationPlan plan)
        {
            var baseQuery = GetVisibleComponentsQuery()
                .Where(x => x.ComponentType == PcComponentType.Monitor);

            if (plan.NeedsHighRefreshMonitor)
            {
                baseQuery = baseQuery.Where(x => x.Specifications.Any(s =>
                    s.SpecificationDefinition.Code == "monitor_refresh_rate_hz" &&
                    s.ValueNumber != null &&
                    s.ValueNumber >= 144));
            }

            var candidates = await LoadCandidatesAsync(baseQuery, plan, PcComponentType.Monitor);
            if (!candidates.Any())
            {
                return null;
            }

            return candidates
                .OrderByDescending(x => ScoreMonitorCandidate(x, plan))
                .ThenBy(x => x.Price)
                .FirstOrDefault();
        }

        private async Task<List<ProductModel>> LoadCandidatesAsync(
            IQueryable<ProductModel> query,
            BuildRecommendationPlan plan,
            PcComponentType componentType)
        {
            var budgetCap = plan.GetBudget(componentType);
            if (budgetCap <= 0m)
            {
                return new List<ProductModel>();
            }

            var multipliers = plan.HasHardBudgetCap
                ? new[] { 1m, 1.15m, 1.30m, 1.45m, 1.60m }
                : new[] { 1m, 1.15m, 1.30m };

            foreach (var multiplier in multipliers)
            {
                var cap = Math.Min(plan.TotalBudget, budgetCap * multiplier);
                var candidates = await query
                    .Where(x => x.Price <= cap)
                    .ToListAsync();

                if (candidates.Any())
                {
                    return candidates;
                }
            }

            if (plan.HasHardBudgetCap)
            {
                var fallbackCandidates = await query
                    .Where(x => x.Price <= plan.TotalBudget)
                    .ToListAsync();

                if (fallbackCandidates.Any())
                {
                    return fallbackCandidates;
                }
            }

            return new List<ProductModel>();
        }

        private IQueryable<ProductModel> GetVisibleComponentsQuery()
        {
            return _context.Products
                .AsNoTracking()
                .Include(x => x.Publisher)
                .Include(x => x.Specifications)
                    .ThenInclude(x => x.SpecificationDefinition)
                .WhereVisibleOnStorefront(_context)
                .Where(x => x.Quantity > 0)
                .Where(x => x.ProductType == ProductType.Component || x.ProductType == ProductType.Monitor);
        }

        private static double ScoreCpuCandidate(ProductModel product, BuildRecommendationPlan plan)
        {
            var benchmark = (double)(GetNumber(product, "cpu_benchmark_score") ?? 0m);
            var cores = (double)(GetNumber(product, "cpu_cores") ?? 0m);
            var threads = (double)(GetNumber(product, "cpu_threads") ?? 0m);
            var price = Math.Max((double)product.Price, 1d);

            var score = benchmark
                + (cores * 900d)
                + (threads * 350d)
                + (benchmark / price * 850000d);

            if (plan.PreferCpuPerformance)
            {
                score += benchmark * 0.18d;
            }

            if (plan.PreferQuietOperation)
            {
                score -= (double)(GetNumber(product, "cpu_tdp_w") ?? 0m) * 12d;
            }

            if (MatchesPreferredBrand(product, plan.PreferredBrand))
            {
                score += 45000d;
            }

            if (plan.IsLightDuty)
            {
                score -= price * 0.03d;
            }

            return score;
        }

        private static double ScoreMainboardCandidate(ProductModel product, BuildRecommendationPlan plan)
        {
            var ramSlots = (double)(GetNumber(product, "mb_ram_slots") ?? 0m);
            var maxRam = (double)(GetNumber(product, "mb_max_ram_gb") ?? 0m);
            var price = Math.Max((double)product.Price, 1d);

            var score = (ramSlots * 800d)
                + (maxRam * 55d)
                + (maxRam / price * 120000d);

            if (plan.PreferMultitasking)
            {
                score += ramSlots * 600d;
            }

            if (plan.HasHardBudgetCap)
            {
                score -= price * 0.02d;
            }

            return score;
        }

        private static double ScoreRamCandidate(ProductModel product, BuildRecommendationPlan plan)
        {
            var capacity = (double)(GetNumber(product, "ram_capacity_gb") ?? 0m);
            var bus = (double)(GetNumber(product, "ram_bus_mhz") ?? 0m);
            var targetGap = Math.Abs(capacity - plan.TargetRamGb);
            var price = Math.Max((double)product.Price, 1d);

            var score = (capacity * 1100d)
                + (bus * 1.5d)
                + (capacity / price * 180000d)
                - (targetGap * 650d);

            if (plan.PreferMultitasking || plan.IsWorkstationLike)
            {
                score += capacity * 320d;
            }

            if (plan.HasHardBudgetCap)
            {
                score -= price * 0.02d;
            }

            return score;
        }

        private static double ScoreGpuCandidate(ProductModel product, BuildRecommendationPlan plan)
        {
            var benchmark = (double)(GetNumber(product, "gpu_benchmark_score") ?? 0m);
            var vram = (double)(GetNumber(product, "gpu_vram_gb") ?? 0m);
            var price = Math.Max((double)product.Price, 1d);

            var score = benchmark
                + (vram * 2200d)
                + (benchmark / price * 900000d);

            if (plan.PreferGpuPerformance)
            {
                score += benchmark * 0.22d;
            }

            if (MatchesPreferredBrand(product, plan.PreferredBrand))
            {
                score += 55000d;
            }

            if (plan.HasHardBudgetCap)
            {
                score -= price * 0.015d;
            }

            return score;
        }

        private static double ScorePsuCandidate(ProductModel product, BuildRecommendationPlan plan, decimal minimumPsuWatt)
        {
            var watt = (double)(GetNumber(product, "psu_watt") ?? 0m);
            var price = Math.Max((double)product.Price, 1d);
            var headroom = Math.Max(0d, watt - (double)minimumPsuWatt);

            var score = (Math.Min(headroom, 250d) * 32d) - (price * 0.01d);

            if ((GetText(product, "psu_efficiency") ?? string.Empty).Contains("gold", StringComparison.OrdinalIgnoreCase))
            {
                score += 9000d;
            }

            if (plan.PreferQuietOperation)
            {
                score += 2500d;
            }

            return score;
        }

        private static double ScoreSsdCandidate(ProductModel product, BuildRecommendationPlan plan)
        {
            var capacity = (double)(GetNumber(product, "ssd_capacity_gb") ?? 0m);
            var targetGap = Math.Abs(capacity - plan.TargetSsdGb);
            var price = Math.Max((double)product.Price, 1d);
            var score = (capacity * 160d)
                + (capacity / price * 240000d)
                - (targetGap * 75d);

            var storageType = GetText(product, "ssd_storage_type");
            var storageInterface = GetText(product, "ssd_interface");
            if (string.Equals(storageType, "NVMe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(storageInterface, "NVMe", StringComparison.OrdinalIgnoreCase))
            {
                score += 8500d;
            }

            if (plan.PreferStorageCapacity)
            {
                score += capacity * 90d;
            }

            if (plan.HasHardBudgetCap)
            {
                score -= price * 0.015d;
            }

            return score;
        }

        private static double ScoreCaseCandidate(ProductModel product, BuildRecommendationPlan plan)
        {
            var price = Math.Max((double)product.Price, 1d);
            var maxGpuLength = (double)(GetNumber(product, "case_max_gpu_length_mm") ?? 0m);
            var score = maxGpuLength * 6d;

            if (plan.PreferQuietOperation)
            {
                score += 1500d;
            }

            return score - (price * 0.015d);
        }

        private static double ScoreMonitorCandidate(ProductModel product, BuildRecommendationPlan plan)
        {
            var refresh = (double)(GetNumber(product, "monitor_refresh_rate_hz") ?? 0m);
            var size = (double)(GetNumber(product, "monitor_size_inch") ?? 0m);
            var price = Math.Max((double)product.Price, 1d);
            var score = refresh * 80d + size * 140d;

            if (plan.PreferGpuPerformance || plan.NeedsHighRefreshMonitor)
            {
                score += refresh * 55d;
            }

            return score - (price * 0.02d);
        }

        private static bool HasCoreBuild(IReadOnlyCollection<PcBuildCheckItemDto> items, BuildRecommendationPlan plan)
        {
            var required = new HashSet<PcComponentType>
            {
                PcComponentType.CPU,
                PcComponentType.Mainboard,
                PcComponentType.RAM,
                PcComponentType.SSD,
                PcComponentType.PSU,
                PcComponentType.Case
            };

            if (plan.NeedsDedicatedGpu)
            {
                required.Add(PcComponentType.GPU);
            }

            var selected = items
                .Select(x => x.ComponentType)
                .ToHashSet();

            return required.All(selected.Contains);
        }

        private static bool SupportsMainboard(ProductModel pcCase, ProductModel? mainboard)
        {
            if (mainboard == null)
            {
                return true;
            }

            var mainboardSize = GetText(mainboard, "mb_form_factor");
            var supportedSizes = GetJsonList(pcCase, "case_supported_mb_sizes");

            return string.IsNullOrWhiteSpace(mainboardSize)
                || !supportedSizes.Any()
                || supportedSizes.Any(x => string.Equals(x, mainboardSize, StringComparison.OrdinalIgnoreCase));
        }

        private static bool SupportsGpuLength(ProductModel pcCase, ProductModel? gpu)
        {
            if (gpu == null)
            {
                return true;
            }

            var maxGpuLength = GetNumber(pcCase, "case_max_gpu_length_mm");
            var gpuLength = GetNumber(gpu, "gpu_length_mm");

            return !maxGpuLength.HasValue
                || !gpuLength.HasValue
                || gpuLength <= maxGpuLength;
        }

        private static decimal GetSelectedPrice(
            PcComponentType componentType,
            ProductModel? cpu,
            ProductModel? mainboard,
            ProductModel? ram,
            ProductModel? gpu,
            ProductModel? psu,
            ProductModel? ssd,
            ProductModel? pcCase,
            ProductModel? monitor)
        {
            return componentType switch
            {
                PcComponentType.CPU => cpu?.Price ?? 0m,
                PcComponentType.Mainboard => mainboard?.Price ?? 0m,
                PcComponentType.RAM => ram?.Price ?? 0m,
                PcComponentType.GPU => gpu?.Price ?? 0m,
                PcComponentType.PSU => psu?.Price ?? 0m,
                PcComponentType.SSD => ssd?.Price ?? 0m,
                PcComponentType.Case => pcCase?.Price ?? 0m,
                PcComponentType.Monitor => monitor?.Price ?? 0m,
                _ => 0m
            };
        }

        private static bool MatchesPreferredBrand(ProductModel product, string preferredBrand)
        {
            if (string.IsNullOrWhiteSpace(preferredBrand))
            {
                return false;
            }

            return product.Name.Contains(preferredBrand, StringComparison.OrdinalIgnoreCase)
                || (product.Publisher?.Name?.Contains(preferredBrand, StringComparison.OrdinalIgnoreCase) == true);
        }

        private static string? GetText(ProductModel? product, string code)
        {
            return product?.Specifications
                .FirstOrDefault(x => x.SpecificationDefinition?.Code == code)
                ?.ValueText;
        }

        private static decimal? GetNumber(ProductModel? product, string code)
        {
            return product?.Specifications
                .FirstOrDefault(x => x.SpecificationDefinition?.Code == code)
                ?.ValueNumber;
        }

        private static List<string> GetJsonList(ProductModel? product, string code)
        {
            var raw = product?.Specifications
                .FirstOrDefault(x => x.SpecificationDefinition?.Code == code)
                ?.ValueJson;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private sealed class BuildRecommendationPlan
        {
            public string Purpose { get; init; } = string.Empty;
            public string PreferredBrand { get; init; } = string.Empty;
            public decimal TotalBudget { get; init; }
            public bool HasHardBudgetCap { get; init; }
            public bool NeedsDedicatedGpu { get; init; }
            public bool NeedsHighRefreshMonitor { get; init; }
            public bool IncludeMonitor => NeedsHighRefreshMonitor;
            public bool IsLightDuty { get; init; }
            public bool IsWorkstationLike { get; init; }
            public bool PreferQuietOperation { get; init; }
            public bool PreferMultitasking { get; init; }
            public bool PreferStorageCapacity { get; init; }
            public bool PreferCpuPerformance { get; init; }
            public bool PreferGpuPerformance { get; init; }
            public int TargetRamGb { get; init; }
            public int TargetSsdGb { get; init; }
            public IReadOnlyDictionary<PcComponentType, decimal> ComponentBudgets { get; init; } = new Dictionary<PcComponentType, decimal>();

            public decimal GetBudget(PcComponentType componentType)
            {
                return ComponentBudgets.TryGetValue(componentType, out var value)
                    ? value
                    : 0m;
            }

            public static BuildRecommendationPlan Create(BuildRequirementProfile profile)
            {
                var purpose = ResolvePurpose(profile);
                var totalBudget = profile.BudgetMax ?? ResolveDefaultBudget(purpose);
                var needsDedicatedGpu = ResolveNeedsDedicatedGpu(profile, purpose);
                var isLightDuty = IsLightDutyPurpose(purpose);
                var isWorkstationLike = IsWorkstationLikePurpose(purpose);
                var preferStorage = string.Equals(profile.PerformancePriority, "Storage Capacity", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(purpose, "Home Server", StringComparison.OrdinalIgnoreCase);
                var preferCpu = string.Equals(profile.PerformancePriority, "CPU Performance", StringComparison.OrdinalIgnoreCase)
                    || isWorkstationLike;
                var preferGpu = string.Equals(profile.PerformancePriority, "GPU Performance", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(purpose, "Gaming", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(purpose, "AI / Machine Learning", StringComparison.OrdinalIgnoreCase);

                return new BuildRecommendationPlan
                {
                    Purpose = purpose,
                    PreferredBrand = profile.PreferredBrand ?? string.Empty,
                    TotalBudget = totalBudget,
                    HasHardBudgetCap = profile.BudgetMax.HasValue,
                    NeedsDedicatedGpu = needsDedicatedGpu,
                    NeedsHighRefreshMonitor = profile.NeedsMonitorHighRefresh,
                    IsLightDuty = isLightDuty,
                    IsWorkstationLike = isWorkstationLike,
                    PreferQuietOperation = string.Equals(profile.PerformancePriority, "Quiet Operation", StringComparison.OrdinalIgnoreCase),
                    PreferMultitasking = string.Equals(profile.PerformancePriority, "Multitasking", StringComparison.OrdinalIgnoreCase),
                    PreferStorageCapacity = preferStorage,
                    PreferCpuPerformance = preferCpu,
                    PreferGpuPerformance = preferGpu,
                    TargetRamGb = ResolveTargetRamGb(purpose, totalBudget),
                    TargetSsdGb = ResolveTargetSsdGb(purpose, totalBudget, preferStorage),
                    ComponentBudgets = ResolveComponentBudgets(purpose, totalBudget, needsDedicatedGpu, profile.NeedsMonitorHighRefresh)
                };
            }

            private static string ResolvePurpose(BuildRequirementProfile profile)
            {
                if (!string.IsNullOrWhiteSpace(profile.PrimaryPurpose))
                {
                    return profile.PrimaryPurpose;
                }

                if (!string.IsNullOrWhiteSpace(profile.GameTitle))
                {
                    return "Gaming";
                }

                if (profile.NeedsEditing)
                {
                    return "Content Creation";
                }

                if (profile.NeedsStreaming)
                {
                    return "Streaming";
                }

                return "General";
            }

            private static decimal ResolveDefaultBudget(string purpose)
            {
                return purpose switch
                {
                    "Office" => 12_000_000m,
                    "Study" => 12_000_000m,
                    "Home Entertainment" => 14_000_000m,
                    "Programming" => 18_000_000m,
                    "Music Production" => 20_000_000m,
                    "Trading / Multi Monitor" => 18_000_000m,
                    "Home Server" => 18_000_000m,
                    "Photo Editing" => 22_000_000m,
                    "CAD / 3D" => 28_000_000m,
                    "AI / Machine Learning" => 35_000_000m,
                    "Content Creation" => 28_000_000m,
                    "Streaming" => 24_000_000m,
                    "Gaming" => 24_000_000m,
                    _ => 18_000_000m
                };
            }

            private static bool ResolveNeedsDedicatedGpu(BuildRequirementProfile profile, string purpose)
            {
                return purpose switch
                {
                    "Gaming" => true,
                    "Content Creation" => true,
                    "CAD / 3D" => true,
                    "AI / Machine Learning" => true,
                    "Streaming" => true,
                    _ => profile.NeedsStreaming || (!string.IsNullOrWhiteSpace(profile.GameTitle))
                };
            }

            private static bool IsLightDutyPurpose(string purpose)
            {
                return purpose is "Office"
                    or "Study"
                    or "Home Entertainment"
                    or "Trading / Multi Monitor";
            }

            private static bool IsWorkstationLikePurpose(string purpose)
            {
                return purpose is "Content Creation"
                    or "Music Production"
                    or "Programming"
                    or "Photo Editing"
                    or "CAD / 3D"
                    or "AI / Machine Learning"
                    or "Home Server"
                    or "Streaming";
            }

            private static int ResolveTargetRamGb(string purpose, decimal totalBudget)
            {
                if (purpose is "Office" or "Study" or "Home Entertainment")
                {
                    return 16;
                }

                if (purpose is "Programming" or "Music Production" or "Trading / Multi Monitor" or "Home Server")
                {
                    return totalBudget >= 20_000_000m ? 32 : 16;
                }

                if (purpose is "Gaming")
                {
                    return totalBudget >= 25_000_000m ? 32 : 16;
                }

                return totalBudget >= 30_000_000m ? 32 : 16;
            }

            private static int ResolveTargetSsdGb(string purpose, decimal totalBudget, bool preferStorage)
            {
                if (purpose == "Home Server" || preferStorage)
                {
                    return totalBudget >= 20_000_000m ? 2000 : 1000;
                }

                if (purpose is "Music Production" or "Content Creation" or "CAD / 3D" or "AI / Machine Learning" or "Programming")
                {
                    return 1000;
                }

                if (purpose is "Home Entertainment")
                {
                    return 1000;
                }

                return totalBudget >= 18_000_000m ? 1000 : 512;
            }

            private static IReadOnlyDictionary<PcComponentType, decimal> ResolveComponentBudgets(
                string purpose,
                decimal totalBudget,
                bool needsDedicatedGpu,
                bool includeMonitor)
            {
                var ratios = new Dictionary<PcComponentType, decimal>();

                if (purpose == "Gaming")
                {
                    ratios[PcComponentType.CPU] = includeMonitor ? 0.17m : 0.20m;
                    ratios[PcComponentType.Mainboard] = includeMonitor ? 0.09m : 0.10m;
                    ratios[PcComponentType.RAM] = 0.10m;
                    ratios[PcComponentType.GPU] = includeMonitor ? 0.28m : 0.34m;
                    ratios[PcComponentType.PSU] = 0.08m;
                    ratios[PcComponentType.SSD] = 0.10m;
                    ratios[PcComponentType.Case] = 0.05m;
                }
                else if (purpose is "AI / Machine Learning" or "CAD / 3D" or "Content Creation" or "Photo Editing")
                {
                    ratios[PcComponentType.CPU] = includeMonitor ? 0.20m : 0.24m;
                    ratios[PcComponentType.Mainboard] = 0.10m;
                    ratios[PcComponentType.RAM] = 0.16m;
                    ratios[PcComponentType.GPU] = purpose == "AI / Machine Learning"
                        ? (includeMonitor ? 0.24m : 0.30m)
                        : (includeMonitor ? 0.18m : 0.22m);
                    ratios[PcComponentType.PSU] = 0.08m;
                    ratios[PcComponentType.SSD] = 0.10m;
                    ratios[PcComponentType.Case] = 0.06m;
                }
                else if (purpose is "Music Production" or "Programming" or "Home Server")
                {
                    ratios[PcComponentType.CPU] = 0.28m;
                    ratios[PcComponentType.Mainboard] = 0.14m;
                    ratios[PcComponentType.RAM] = 0.18m;
                    ratios[PcComponentType.SSD] = purpose == "Home Server" ? 0.22m : 0.16m;
                    ratios[PcComponentType.PSU] = 0.10m;
                    ratios[PcComponentType.Case] = 0.08m;
                }
                else
                {
                    ratios[PcComponentType.CPU] = includeMonitor ? 0.20m : 0.24m;
                    ratios[PcComponentType.Mainboard] = 0.14m;
                    ratios[PcComponentType.RAM] = 0.16m;
                    ratios[PcComponentType.SSD] = 0.18m;
                    ratios[PcComponentType.PSU] = 0.10m;
                    ratios[PcComponentType.Case] = 0.08m;
                }

                if (needsDedicatedGpu && !ratios.ContainsKey(PcComponentType.GPU))
                {
                    ratios[PcComponentType.GPU] = includeMonitor ? 0.12m : 0.16m;
                    ratios[PcComponentType.CPU] = Math.Max(0.16m, ratios.GetValueOrDefault(PcComponentType.CPU) - 0.04m);
                    ratios[PcComponentType.SSD] = Math.Max(0.10m, ratios.GetValueOrDefault(PcComponentType.SSD) - 0.02m);
                }

                if (includeMonitor)
                {
                    ratios[PcComponentType.Monitor] = 0.14m;
                }

                return ratios.ToDictionary(
                    x => x.Key,
                    x => Math.Floor(totalBudget * x.Value));
            }
        }
    }
}
