using Eshop.Helpers;
using Eshop.Models;
using Eshop.Models.Configurations;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Eshop.Services
{
    public sealed class PcBuildChatProductSuggestionService
    {
        private readonly DataContext _context;
        private readonly RagClient _ragClient;
        private readonly string _catalogNamespace;

        public PcBuildChatProductSuggestionService(
            DataContext context,
            RagClient ragClient,
            IOptions<RagServiceOptions> ragOptions)
        {
            _context = context;
            _ragClient = ragClient;
            _catalogNamespace = ragOptions.Value.CatalogNamespace;
        }

        internal async Task<List<PcBuildChatSuggestedProductDto>> BuildSuggestionsAsync(
            PcBuildChatIntentAnalysis analysis,
            bool hasCurrentBuild,
            PcBuildCheckRequest buildRequest,
            IReadOnlyList<PcBuildChatSelectedProduct> selectedProducts,
            PcBuildCheckResponse checkResult,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            if (!analysis.WantsProductSuggestions)
            {
                return new List<PcBuildChatSuggestedProductDto>();
            }

            if (!hasCurrentBuild
                && analysis.Intent == PcBuildChatIntentKind.NewBuild
                && buildRequest.Items != null
                && buildRequest.Items.Any())
            {
                return await ResolveBuildSuggestionCardsAsync(buildRequest.Items, cancellationToken);
            }

            var targetComponents = ResolveTargetComponents(analysis, selectedProducts, checkResult);
            if (!targetComponents.Any())
            {
                return new List<PcBuildChatSuggestedProductDto>();
            }

            var ragRankMap = await SearchCatalogCandidatesAsync(
                analysis,
                userMessage,
                targetComponents,
                selectedProducts,
                cancellationToken);

            var baseQuery = _context.Products
                .AsNoTracking()
                .Include(x => x.Publisher)
                .Include(x => x.ProductImages)
                .Include(x => x.Specifications)
                    .ThenInclude(x => x.SpecificationDefinition)
                .WhereVisibleOnStorefront(_context)
                .Where(x => x.ComponentType.HasValue && targetComponents.Contains(x.ComponentType.Value));

            var primaryProducts = ragRankMap.Any()
                ? await baseQuery
                    .Where(x => ragRankMap.Keys.Contains(x.Id))
                    .ToListAsync(cancellationToken)
                : new List<ProductModel>();

            var fallbackProducts = await baseQuery
                .OrderByDescending(x => x.Sold)
                .ThenByDescending(x => x.Price)
                .Take(48)
                .ToListAsync(cancellationToken);

            var combinedProducts = primaryProducts
                .Concat(fallbackProducts)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToList();

            var componentBudget = ResolveComponentBudget(analysis, targetComponents.Count);
            var scoredProducts = combinedProducts
                .Select(product => new
                {
                    Product = product,
                    Score = ScoreProduct(product, analysis, selectedProducts, ragRankMap, componentBudget, hasCurrentBuild, checkResult)
                })
                .Where(x => x.Score > -25)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Product.Price)
                .ToList();

            var results = new List<PcBuildChatSuggestedProductDto>();
            var perTypeCount = new Dictionary<PcComponentType, int>();
            var maxPerType = analysis.Intent == PcBuildChatIntentKind.ComponentRecommendation ? 3 : 2;
            var maxTotal = analysis.Intent == PcBuildChatIntentKind.ComponentRecommendation ? 6 : 5;

            foreach (var item in scoredProducts)
            {
                if (!item.Product.ComponentType.HasValue || item.Product.ComponentType.Value == PcComponentType.None)
                {
                    continue;
                }

                var componentType = item.Product.ComponentType.Value;
                var currentCount = perTypeCount.GetValueOrDefault(componentType);
                if (currentCount >= maxPerType)
                {
                    continue;
                }

                results.Add(new PcBuildChatSuggestedProductDto
                {
                    ComponentType = componentType.ToString(),
                    Reason = BuildReason(item.Product, analysis, selectedProducts, checkResult),
                    Product = PcBuilderProductMapper.ToCardDto(item.Product)
                });

                perTypeCount[componentType] = currentCount + 1;

                if (results.Count >= maxTotal)
                {
                    break;
                }
            }

            return results;
        }

        private async Task<List<PcBuildChatSuggestedProductDto>> ResolveBuildSuggestionCardsAsync(
            IReadOnlyCollection<PcBuildCheckItemDto> items,
            CancellationToken cancellationToken)
        {
            var productIds = items
                .Where(x => x.ProductId > 0)
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            if (!productIds.Any())
            {
                return new List<PcBuildChatSuggestedProductDto>();
            }

            var products = await _context.Products
                .AsNoTracking()
                .Include(x => x.Publisher)
                .Include(x => x.ProductImages)
                .Include(x => x.Specifications)
                    .ThenInclude(x => x.SpecificationDefinition)
                .WhereVisibleOnStorefront(_context)
                .Where(x => productIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            return items
                .Where(x => products.ContainsKey(x.ProductId))
                .Select(x =>
                {
                    var product = products[x.ProductId];
                    var quantityText = x.Quantity > 1 ? $" - so luong {x.Quantity}" : string.Empty;

                    return new PcBuildChatSuggestedProductDto
                    {
                        ComponentType = x.ComponentType.ToString(),
                        Reason = $"Linh kien trong cau hinh goi y{quantityText}.",
                        Product = PcBuilderProductMapper.ToCardDto(product)
                    };
                })
                .Take(8)
                .ToList();
        }

        private async Task<Dictionary<int, int>> SearchCatalogCandidatesAsync(
            PcBuildChatIntentAnalysis analysis,
            string userMessage,
            IReadOnlyCollection<PcComponentType> targetComponents,
            IReadOnlyList<PcBuildChatSelectedProduct> selectedProducts,
            CancellationToken cancellationToken)
        {
            try
            {
                var queryParts = new List<string>
                {
                    userMessage
                };

                if (targetComponents.Any())
                {
                    queryParts.Add("Linh kien can tim: " + string.Join(", ", targetComponents));
                }

                if (!string.IsNullOrWhiteSpace(analysis.Requirement.GameTitle))
                {
                    queryParts.Add($"Game: {analysis.Requirement.GameTitle}");
                }

                if (!string.IsNullOrWhiteSpace(analysis.Requirement.PrimaryPurpose))
                {
                    queryParts.Add($"Muc dich: {analysis.Requirement.PrimaryPurpose}");
                }

                if (analysis.Requirement.BudgetMax.HasValue)
                {
                    queryParts.Add($"Ngan sach toi da: {analysis.Requirement.BudgetMax.Value}");
                }

                if (!string.IsNullOrWhiteSpace(analysis.Requirement.ResolutionTarget))
                {
                    queryParts.Add($"Do phan giai: {analysis.Requirement.ResolutionTarget}");
                }

                if (selectedProducts.Any())
                {
                    queryParts.Add("Build hien tai: " + string.Join("; ", selectedProducts.Select(x => x.Product.Name)));
                }

                var response = await _ragClient.SearchAsync(
                    string.Join(". ", queryParts.Where(x => !string.IsNullOrWhiteSpace(x))),
                    _catalogNamespace,
                    k: 10,
                    minScore: 0.08,
                    cancellationToken: cancellationToken);

                var rankedIds = new Dictionary<int, int>();
                var rank = 0;

                foreach (var result in response?.Results ?? new List<RagSearchItem>())
                {
                    if (!TryParseProductId(result.DocId, out var productId))
                    {
                        continue;
                    }

                    if (!rankedIds.ContainsKey(productId))
                    {
                        rankedIds[productId] = rank++;
                    }
                }

                return rankedIds;
            }
            catch
            {
                return new Dictionary<int, int>();
            }
        }

        private static List<PcComponentType> ResolveTargetComponents(
            PcBuildChatIntentAnalysis analysis,
            IReadOnlyList<PcBuildChatSelectedProduct> selectedProducts,
            PcBuildCheckResponse checkResult)
        {
            if (analysis.TargetComponents.Any())
            {
                return analysis.TargetComponents
                    .Distinct()
                    .ToList();
            }

            var inferred = new List<PcComponentType>();
            var messages = checkResult?.Messages ?? new List<CompatibilityMessageDto>();

            foreach (var message in messages)
            {
                var text = (message.Message ?? string.Empty).ToLowerInvariant();

                if (text.Contains("socket"))
                {
                    inferred.Add(selectedProducts.Any(x => x.Item.ComponentType == PcComponentType.CPU)
                        ? PcComponentType.Mainboard
                        : PcComponentType.CPU);
                }

                if (text.Contains("ram"))
                {
                    inferred.Add(PcComponentType.RAM);
                }

                if (text.Contains("nguồn") || text.Contains("nguon") || text.Contains("psu"))
                {
                    inferred.Add(PcComponentType.PSU);
                }

                if (text.Contains("case"))
                {
                    inferred.Add(PcComponentType.Case);
                }

                if (text.Contains("tản") || text.Contains("tan"))
                {
                    inferred.Add(PcComponentType.Cooler);
                }
            }

            return inferred
                .Distinct()
                .Where(x => x != PcComponentType.None)
                .ToList();
        }

        private static decimal? ResolveComponentBudget(PcBuildChatIntentAnalysis analysis, int targetComponentCount)
        {
            if (targetComponentCount != 1)
            {
                return null;
            }

            if (!analysis.Requirement.BudgetMax.HasValue)
            {
                return null;
            }

            return analysis.Intent switch
            {
                PcBuildChatIntentKind.ComponentRecommendation => analysis.Requirement.BudgetMax.Value,
                PcBuildChatIntentKind.UpgradeCurrentBuild => analysis.Requirement.BudgetMax.Value,
                _ => null
            };
        }

        private static double ScoreProduct(
            ProductModel product,
            PcBuildChatIntentAnalysis analysis,
            IReadOnlyList<PcBuildChatSelectedProduct> selectedProducts,
            IReadOnlyDictionary<int, int> ragRankMap,
            decimal? componentBudget,
            bool hasCurrentBuild,
            PcBuildCheckResponse checkResult)
        {
            if (!product.ComponentType.HasValue)
            {
                return -100;
            }

            var score = 10d;

            if (ragRankMap.TryGetValue(product.Id, out var rank))
            {
                score += Math.Max(0, 72 - (rank * 8));
            }

            score += Math.Min(product.Sold / 10d, 12d);

            if (product.Quantity > 0)
            {
                score += 6;
            }

            if (!string.IsNullOrWhiteSpace(analysis.Requirement.PreferredBrand)
                && string.Equals(product.Publisher?.Name, analysis.Requirement.PreferredBrand, StringComparison.OrdinalIgnoreCase))
            {
                score += 7;
            }

            if (componentBudget.HasValue)
            {
                if (product.Price <= componentBudget.Value)
                {
                    var ratio = componentBudget.Value == 0 ? 0 : Math.Abs((double)((componentBudget.Value - product.Price) / componentBudget.Value));
                    score += Math.Max(0, 14 - (ratio * 18));
                }
                else
                {
                    score -= Math.Min((double)((product.Price - componentBudget.Value) / Math.Max(componentBudget.Value, 1m)) * 20, 18);
                }
            }

            score += ScoreByComponentFocus(product, analysis);

            if (hasCurrentBuild)
            {
                score += ScoreCompatibility(product, selectedProducts, checkResult);

                if (selectedProducts.Any(x => x.Item.ComponentType == product.ComponentType && x.Product.Id == product.Id))
                {
                    score -= 10;
                }
            }

            return score;
        }

        private static double ScoreByComponentFocus(ProductModel product, PcBuildChatIntentAnalysis analysis)
        {
            var type = product.ComponentType ?? PcComponentType.None;
            var score = 0d;

            if (type == PcComponentType.CPU)
            {
                score += Math.Min((double)(GetNumber(product, "cpu_benchmark_score") ?? 0) / 1000d, 24d);
                score += Math.Min((double)(GetNumber(product, "cpu_threads") ?? 0), 16d) * 0.35d;

                if (analysis.Requirement.NeedsEditing || analysis.Requirement.NeedsStreaming)
                {
                    score += Math.Min((double)(GetNumber(product, "cpu_cores") ?? 0), 16d) * 0.5d;
                }
            }
            else if (type == PcComponentType.GPU)
            {
                score += Math.Min((double)(GetNumber(product, "gpu_benchmark_score") ?? 0) / 1000d, 26d);
                score += Math.Min((double)(GetNumber(product, "gpu_vram_gb") ?? 0), 24d) * 0.8d;

                if (!string.IsNullOrWhiteSpace(analysis.Requirement.ResolutionTarget)
                    && analysis.Requirement.ResolutionTarget.Contains("4K", StringComparison.OrdinalIgnoreCase))
                {
                    score += Math.Min((double)(GetNumber(product, "gpu_vram_gb") ?? 0), 24d) * 0.5d;
                }
            }
            else if (type == PcComponentType.RAM)
            {
                score += Math.Min((double)(GetNumber(product, "ram_capacity_gb") ?? 0), 64d) * 0.4d;
                score += Math.Min((double)(GetNumber(product, "ram_bus_mhz") ?? 0), 8000d) / 1000d;

                if (analysis.Requirement.NeedsEditing || analysis.Requirement.NeedsStreaming)
                {
                    score += Math.Min((double)(GetNumber(product, "ram_capacity_gb") ?? 0), 64d) * 0.25d;
                }
            }
            else if (type == PcComponentType.SSD)
            {
                score += Math.Min((double)(GetNumber(product, "ssd_capacity_gb") ?? 0), 4000d) / 220d;

                if (EqualsIgnoreCase(GetText(product, "ssd_interface"), "NVMe")
                    || EqualsIgnoreCase(GetText(product, "ssd_storage_type"), "NVMe"))
                {
                    score += 4;
                }
            }
            else if (type == PcComponentType.PSU)
            {
                score += Math.Min((double)(GetNumber(product, "psu_watt") ?? 0), 1600d) / 80d;

                var efficiency = GetText(product, "psu_efficiency");
                if (!string.IsNullOrWhiteSpace(efficiency) && efficiency.Contains("gold", StringComparison.OrdinalIgnoreCase))
                {
                    score += 3;
                }
            }
            else if (type == PcComponentType.Monitor)
            {
                score += Math.Min((double)(GetNumber(product, "monitor_refresh_rate_hz") ?? 0), 360d) / 18d;

                if (analysis.Requirement.NeedsMonitorHighRefresh)
                {
                    score += Math.Min((double)(GetNumber(product, "monitor_refresh_rate_hz") ?? 0), 360d) / 24d;
                }
            }

            return score;
        }

        private static double ScoreCompatibility(
            ProductModel candidate,
            IReadOnlyList<PcBuildChatSelectedProduct> selectedProducts,
            PcBuildCheckResponse checkResult)
        {
            if (!candidate.ComponentType.HasValue)
            {
                return 0;
            }

            var cpu = GetSelectedProduct(selectedProducts, PcComponentType.CPU);
            var mainboard = GetSelectedProduct(selectedProducts, PcComponentType.Mainboard);
            var gpu = GetSelectedProduct(selectedProducts, PcComponentType.GPU);
            var psu = GetSelectedProduct(selectedProducts, PcComponentType.PSU);
            var pcCase = GetSelectedProduct(selectedProducts, PcComponentType.Case);
            var cooler = GetSelectedProduct(selectedProducts, PcComponentType.Cooler);
            var targetType = candidate.ComponentType.Value;

            switch (targetType)
            {
                case PcComponentType.CPU:
                    if (mainboard != null)
                    {
                        var cpuSocket = GetText(candidate, "cpu_socket");
                        var mbSocket = GetText(mainboard, "mb_socket");
                        if (!string.IsNullOrWhiteSpace(cpuSocket)
                            && !string.IsNullOrWhiteSpace(mbSocket)
                            && !EqualsIgnoreCase(cpuSocket, mbSocket))
                        {
                            return -40;
                        }
                    }
                    return 12;

                case PcComponentType.Mainboard:
                    if (cpu != null)
                    {
                        var cpuSocket = GetText(cpu, "cpu_socket");
                        var mbSocket = GetText(candidate, "mb_socket");
                        if (!string.IsNullOrWhiteSpace(cpuSocket)
                            && !string.IsNullOrWhiteSpace(mbSocket)
                            && !EqualsIgnoreCase(cpuSocket, mbSocket))
                        {
                            return -40;
                        }
                    }

                    if (pcCase != null)
                    {
                        var mbSize = GetText(candidate, "mb_form_factor");
                        var supportedSizes = GetJsonList(pcCase, "case_supported_mb_sizes");
                        if (!string.IsNullOrWhiteSpace(mbSize)
                            && supportedSizes.Any()
                            && !supportedSizes.Any(x => EqualsIgnoreCase(x, mbSize)))
                        {
                            return -28;
                        }
                    }

                    return 14;

                case PcComponentType.RAM:
                    if (mainboard != null)
                    {
                        var ramType = GetText(candidate, "ram_type");
                        var mbRamType = GetText(mainboard, "mb_ram_type");
                        if (!string.IsNullOrWhiteSpace(ramType)
                            && !string.IsNullOrWhiteSpace(mbRamType)
                            && !EqualsIgnoreCase(ramType, mbRamType))
                        {
                            return -36;
                        }
                    }
                    return 12;

                case PcComponentType.GPU:
                    if (pcCase != null)
                    {
                        var gpuLength = GetNumber(candidate, "gpu_length_mm");
                        var caseMaxGpu = GetNumber(pcCase, "case_max_gpu_length_mm");
                        if (gpuLength.HasValue && caseMaxGpu.HasValue && gpuLength > caseMaxGpu)
                        {
                            return -28;
                        }
                    }
                    return 10;

                case PcComponentType.PSU:
                    {
                        var score = 8d;
                        if (gpu != null)
                        {
                            var recommendedPsu = GetNumber(gpu, "gpu_recommended_psu_w");
                            var psuWatt = GetNumber(candidate, "psu_watt");
                            if (recommendedPsu.HasValue && psuWatt.HasValue)
                            {
                                if (psuWatt < recommendedPsu)
                                {
                                    return -35;
                                }

                                score += 5;
                            }
                        }

                        if (pcCase != null)
                        {
                            var psuStandard = GetText(candidate, "psu_standard");
                            var casePsuStandard = GetText(pcCase, "case_psu_standard");
                            if (!string.IsNullOrWhiteSpace(psuStandard)
                                && !string.IsNullOrWhiteSpace(casePsuStandard)
                                && !EqualsIgnoreCase(psuStandard, casePsuStandard))
                            {
                                score -= 10;
                            }
                        }

                        if (checkResult.EstimatedPower > 0)
                        {
                            var psuWatt = GetNumber(candidate, "psu_watt");
                            if (psuWatt.HasValue && psuWatt >= checkResult.EstimatedPower * 1.25m)
                            {
                                score += 4;
                            }
                        }

                        return score;
                    }

                case PcComponentType.Case:
                    {
                        var score = 8d;

                        if (mainboard != null)
                        {
                            var mbSize = GetText(mainboard, "mb_form_factor");
                            var supportedSizes = GetJsonList(candidate, "case_supported_mb_sizes");
                            if (!string.IsNullOrWhiteSpace(mbSize)
                                && supportedSizes.Any()
                                && !supportedSizes.Any(x => EqualsIgnoreCase(x, mbSize)))
                            {
                                return -30;
                            }
                        }

                        if (gpu != null)
                        {
                            var gpuLength = GetNumber(gpu, "gpu_length_mm");
                            var caseMaxGpu = GetNumber(candidate, "case_max_gpu_length_mm");
                            if (gpuLength.HasValue && caseMaxGpu.HasValue && gpuLength > caseMaxGpu)
                            {
                                return -28;
                            }
                        }

                        if (cooler != null)
                        {
                            var coolerHeight = GetNumber(cooler, "cooler_height_mm");
                            var caseMaxCooler = GetNumber(candidate, "case_max_cooler_height_mm");
                            if (coolerHeight.HasValue && caseMaxCooler.HasValue && coolerHeight > caseMaxCooler)
                            {
                                return -22;
                            }
                        }

                        if (psu != null)
                        {
                            var psuStandard = GetText(psu, "psu_standard");
                            var casePsuStandard = GetText(candidate, "case_psu_standard");
                            if (!string.IsNullOrWhiteSpace(psuStandard)
                                && !string.IsNullOrWhiteSpace(casePsuStandard)
                                && !EqualsIgnoreCase(psuStandard, casePsuStandard))
                            {
                                score -= 10;
                            }
                        }

                        return score;
                    }

                case PcComponentType.Cooler:
                    if (pcCase != null)
                    {
                        var coolerHeight = GetNumber(candidate, "cooler_height_mm");
                        var caseMaxCooler = GetNumber(pcCase, "case_max_cooler_height_mm");
                        if (coolerHeight.HasValue && caseMaxCooler.HasValue && coolerHeight > caseMaxCooler)
                        {
                            return -24;
                        }
                    }
                    return 10;

                default:
                    return 6;
            }
        }

        private static string BuildReason(
            ProductModel product,
            PcBuildChatIntentAnalysis analysis,
            IReadOnlyList<PcBuildChatSelectedProduct> selectedProducts,
            PcBuildCheckResponse checkResult)
        {
            var componentType = product.ComponentType ?? PcComponentType.None;

            if (analysis.Intent == PcBuildChatIntentKind.ComponentRecommendation)
            {
                return componentType switch
                {
                    PcComponentType.CPU => "CPU nay hop voi truy van va uu tien hieu nang hien tai.",
                    PcComponentType.Mainboard => "Mainboard nay la ung vien tot de ghep voi cau hinh ban dang hoi.",
                    PcComponentType.RAM => "Bo RAM nay phu hop de nang cap nhanh va de chon vao builder.",
                    PcComponentType.GPU => "GPU nay noi bat trong tam gia va nhu cau ban vua mo ta.",
                    PcComponentType.PSU => "PSU nay la lua chon an toan hon cho nhu cau cong suat hien tai.",
                    PcComponentType.Case => "Case nay de lap vao build va kha hop cho nhu cau thong dung.",
                    _ => "San pham nay khop voi cau hoi va du lieu build hien tai."
                };
            }

            if (componentType == PcComponentType.Mainboard)
            {
                var cpu = GetSelectedProduct(selectedProducts, PcComponentType.CPU);
                var cpuSocket = GetText(cpu, "cpu_socket");
                var mbSocket = GetText(product, "mb_socket");
                if (!string.IsNullOrWhiteSpace(cpuSocket) && !string.IsNullOrWhiteSpace(mbSocket) && EqualsIgnoreCase(cpuSocket, mbSocket))
                {
                    return "Mainboard nay khop socket voi CPU hien tai.";
                }
            }

            if (componentType == PcComponentType.RAM)
            {
                var mainboard = GetSelectedProduct(selectedProducts, PcComponentType.Mainboard);
                var ramType = GetText(product, "ram_type");
                var mbRamType = GetText(mainboard, "mb_ram_type");
                if (!string.IsNullOrWhiteSpace(ramType) && !string.IsNullOrWhiteSpace(mbRamType) && EqualsIgnoreCase(ramType, mbRamType))
                {
                    return "Bo RAM nay dung chuan ma mainboard hien tai dang ho tro.";
                }
            }

            if (componentType == PcComponentType.PSU)
            {
                var recommended = GetSelectedProduct(selectedProducts, PcComponentType.GPU) != null
                    ? GetNumber(GetSelectedProduct(selectedProducts, PcComponentType.GPU), "gpu_recommended_psu_w")
                    : null;
                var psuWatt = GetNumber(product, "psu_watt");
                if (recommended.HasValue && psuWatt.HasValue && psuWatt >= recommended)
                {
                    return "Cong suat PSU nay de tho hon cho GPU hien tai.";
                }
            }

            if (componentType == PcComponentType.Case)
            {
                return "Case nay la ung vien hop ly neu ban dang can sua loi fit linh kien.";
            }

            if (analysis.Intent == PcBuildChatIntentKind.CompatibilityCheck && checkResult.Messages.Any())
            {
                return "San pham nay duoc uu tien de goi y sua loi tuong thich hien tai.";
            }

            return "San pham nay duoc goi y dua tren cau hoi va build hien tai cua ban.";
        }

        private static ProductModel? GetSelectedProduct(
            IReadOnlyList<PcBuildChatSelectedProduct> selectedProducts,
            PcComponentType componentType)
        {
            return selectedProducts
                .FirstOrDefault(x => x.Item.ComponentType == componentType)
                ?.Product;
        }

        private static bool TryParseProductId(string? docId, out int productId)
        {
            productId = 0;

            if (string.IsNullOrWhiteSpace(docId))
            {
                return false;
            }

            var parts = docId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 2
                   && string.Equals(parts[0], "product", StringComparison.OrdinalIgnoreCase)
                   && int.TryParse(parts[1], out productId);
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
                return JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static bool EqualsIgnoreCase(string? left, string? right)
        {
            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
