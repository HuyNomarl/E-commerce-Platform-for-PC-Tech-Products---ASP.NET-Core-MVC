using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Services
{
    public class PcBuildStorageService : IPcBuildStorageService
    {
        private static readonly PcComponentType[] MultiSlotTypes =
        {
            PcComponentType.RAM,
            PcComponentType.SSD,
            PcComponentType.Monitor
        };

        private readonly DataContext _context;
        private readonly IPcCompatibilityService _compatibilityService;

        public PcBuildStorageService(
            DataContext context,
            IPcCompatibilityService compatibilityService)
        {
            _context = context;
            _compatibilityService = compatibilityService;
        }

        public async Task<PcBuilderBuildDetailDto> BuildDetailAsync(string? buildName, IReadOnlyCollection<PcBuildCheckItemDto> items)
        {
            var normalizedItems = NormalizeItems(items);
            var resolved = await ResolveItemsAsync(normalizedItems);

            var checkResult = normalizedItems.Count == 0
                ? new PcBuildCheckResponse
                {
                    IsValid = true,
                    Messages = new List<CompatibilityMessageDto>
                    {
                        new CompatibilityMessageDto
                        {
                            Level = "info",
                            Message = "Chưa có linh kiện nào trong cấu hình."
                        }
                    }
                }
                : await _compatibilityService.CheckAsync(new PcBuildCheckRequest { Items = normalizedItems.ToList() });

            foreach (var missingMessage in BuildMissingMessages(normalizedItems, resolved.ProductsById))
            {
                checkResult.Messages.Add(missingMessage);
            }

            checkResult.IsValid = !checkResult.Messages.Any(x => string.Equals(x.Level, "error", StringComparison.OrdinalIgnoreCase));

            return new PcBuilderBuildDetailDto
            {
                BuildName = ResolveBuildName(buildName),
                IsValid = checkResult.IsValid,
                TotalPrice = checkResult.TotalPrice,
                EstimatedPower = checkResult.EstimatedPower,
                Messages = checkResult.Messages.Any()
                    ? checkResult.Messages
                    : new List<CompatibilityMessageDto>
                    {
                        new CompatibilityMessageDto
                        {
                            Level = "info",
                            Message = "Cấu hình hiện chưa có cảnh báo tương thích."
                        }
                    },
                Items = resolved.Items
            };
        }

        public async Task<PcBuildImportResultDto> ResolveImportedRowsAsync(string? buildName, IReadOnlyCollection<PcBuildWorkbookRowModel> rows)
        {
            var warnings = new List<string>();
            var normalizedItems = new List<PcBuildCheckItemDto>();
            var latestSingleSlotByType = new Dictionary<PcComponentType, PcBuildCheckItemDto>();
            var rowsByName = rows
                .Where(x => !string.IsNullOrWhiteSpace(x.ProductName))
                .GroupBy(x => x.ProductName!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

            var candidateIds = rows
                .Where(x => x.ProductId.HasValue && x.ProductId.Value > 0)
                .Select(x => x.ProductId!.Value)
                .Distinct()
                .ToList();

            var candidateNames = rowsByName.Keys.ToList();

            var products = await _context.Products
                .AsNoTracking()
                .Include(x => x.Publisher)
                .Include(x => x.ProductImages)
                .Include(x => x.Specifications)
                    .ThenInclude(x => x.SpecificationDefinition)
                .Where(x =>
                    candidateIds.Contains(x.Id) ||
                    candidateNames.Contains(x.Name))
                .ToListAsync();

            var productsById = products.ToDictionary(x => x.Id);
            var productsByName = products
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var matchedProduct = MatchImportedProduct(row, productsById, productsByName, warnings);
                if (matchedProduct == null)
                {
                    warnings.Add(BuildMissingRowWarning(row));
                    continue;
                }

                var componentType = row.ComponentType
                    ?? matchedProduct.ComponentType
                    ?? PcComponentType.None;

                if (componentType == PcComponentType.None)
                {
                    warnings.Add($"Không xác định được loại linh kiện cho sản phẩm \"{matchedProduct.Name}\".");
                    continue;
                }

                var item = new PcBuildCheckItemDto
                {
                    ComponentType = componentType,
                    ProductId = matchedProduct.Id,
                    Quantity = Math.Max(1, row.Quantity)
                };

                if (IsMultiSlot(componentType))
                {
                    normalizedItems.Add(item);
                    continue;
                }

                if (latestSingleSlotByType.ContainsKey(componentType))
                {
                    warnings.Add($"File import có nhiều hơn 1 linh kiện cho ô {componentType}. Hệ thống giữ lại dòng cuối cùng.");
                }

                latestSingleSlotByType[componentType] = item;
            }

            normalizedItems.AddRange(latestSingleSlotByType.Values);
            var mergedItems = NormalizeItems(normalizedItems);
            var detail = await BuildDetailAsync(buildName, mergedItems);

            return new PcBuildImportResultDto
            {
                BuildName = detail.BuildName,
                BuildCode = detail.BuildCode,
                BuildId = detail.BuildId,
                IsValid = detail.IsValid,
                TotalPrice = detail.TotalPrice,
                EstimatedPower = detail.EstimatedPower,
                Messages = detail.Messages,
                Items = detail.Items,
                ImportedRowCount = mergedItems.Sum(x => x.Quantity),
                Warnings = warnings
            };
        }

        public async Task<PcBuildSaveResultDto> SaveAsync(string? buildName, IReadOnlyCollection<PcBuildCheckItemDto> items, string? userId, bool allowInvalidBuild)
        {
            var detail = await BuildDetailAsync(buildName, items);
            if (!detail.Items.Any())
            {
                throw new InvalidOperationException("Chưa có linh kiện hợp lệ để lưu.");
            }

            if (!allowInvalidBuild && !detail.IsValid)
            {
                var errorMessage = detail.Messages.FirstOrDefault(x => string.Equals(x.Level, "error", StringComparison.OrdinalIgnoreCase))
                    ?.Message;
                throw new InvalidOperationException(errorMessage ?? "Cấu hình đang có lỗi tương thích, chưa thể tiếp tục.");
            }

            var build = new PcBuildModel
            {
                BuildName = detail.BuildName,
                TotalPrice = detail.TotalPrice,
                UserId = string.IsNullOrWhiteSpace(userId) ? null : userId
            };

            _context.PcBuilds.Add(build);
            await _context.SaveChangesAsync();

            foreach (var item in detail.Items)
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

            detail.BuildId = build.Id;
            detail.BuildCode = build.BuildCode;

            return new PcBuildSaveResultDto
            {
                BuildId = build.Id,
                BuildCode = build.BuildCode,
                Detail = detail
            };
        }

        public async Task<PcBuilderBuildDetailDto?> GetBuildDetailByIdAsync(int buildId)
        {
            var build = await _context.PcBuilds
                .AsNoTracking()
                .Include(x => x.Items)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x.Publisher)
                .Include(x => x.Items)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x.ProductImages)
                .Include(x => x.Items)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x.Specifications)
                            .ThenInclude(x => x.SpecificationDefinition)
                .FirstOrDefaultAsync(x => x.Id == buildId);

            if (build == null)
            {
                return null;
            }

            return await BuildDetailFromPersistedBuildAsync(build);
        }

        public async Task<PcBuilderBuildDetailDto?> GetBuildDetailByCodeAsync(string buildCode)
        {
            if (string.IsNullOrWhiteSpace(buildCode))
            {
                return null;
            }

            var build = await _context.PcBuilds
                .AsNoTracking()
                .Include(x => x.Items)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x.Publisher)
                .Include(x => x.Items)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x.ProductImages)
                .Include(x => x.Items)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x.Specifications)
                            .ThenInclude(x => x.SpecificationDefinition)
                .FirstOrDefaultAsync(x => x.BuildCode == buildCode.Trim());

            if (build == null)
            {
                return null;
            }

            return await BuildDetailFromPersistedBuildAsync(build);
        }

        private async Task<PcBuilderBuildDetailDto> BuildDetailFromPersistedBuildAsync(PcBuildModel build)
        {
            var items = build.Items
                .Select(x => new PcBuildCheckItemDto
                {
                    ComponentType = x.ComponentType,
                    ProductId = x.ProductId,
                    Quantity = x.Quantity
                })
                .ToList();

            var detail = await BuildDetailAsync(build.BuildName, items);
            detail.BuildId = build.Id;
            detail.BuildCode = build.BuildCode;
            return detail;
        }

        private async Task<(List<PcBuilderResolvedItemDto> Items, Dictionary<int, ProductModel> ProductsById)> ResolveItemsAsync(
            IReadOnlyCollection<PcBuildCheckItemDto> items)
        {
            var productIds = items
                .Where(x => x.ProductId > 0)
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            var products = productIds.Count == 0
                ? new List<ProductModel>()
                : await _context.Products
                    .AsNoTracking()
                    .Include(x => x.Publisher)
                    .Include(x => x.ProductImages)
                    .Include(x => x.Specifications)
                        .ThenInclude(x => x.SpecificationDefinition)
                    .Where(x => productIds.Contains(x.Id))
                    .ToListAsync();

            var productLookup = products.ToDictionary(x => x.Id);
            var resolvedItems = items
                .Where(x => productLookup.ContainsKey(x.ProductId))
                .Select(x => new PcBuilderResolvedItemDto
                {
                    ComponentType = x.ComponentType,
                    ProductId = x.ProductId,
                    Quantity = Math.Max(1, x.Quantity),
                    Product = PcBuilderProductMapper.ToCardDto(productLookup[x.ProductId])
                })
                .OrderBy(x => GetComponentOrder(x.ComponentType))
                .ThenBy(x => x.Product.Name)
                .ToList();

            return (resolvedItems, productLookup);
        }

        private static List<PcBuildCheckItemDto> NormalizeItems(IReadOnlyCollection<PcBuildCheckItemDto> items)
        {
            return (items ?? Array.Empty<PcBuildCheckItemDto>())
                .Where(x => x != null && x.ProductId > 0 && x.ComponentType != PcComponentType.None && x.Quantity > 0)
                .GroupBy(x => new { x.ComponentType, x.ProductId })
                .Select(x => new PcBuildCheckItemDto
                {
                    ComponentType = x.Key.ComponentType,
                    ProductId = x.Key.ProductId,
                    Quantity = x.Sum(y => Math.Max(1, y.Quantity))
                })
                .OrderBy(x => GetComponentOrder(x.ComponentType))
                .ThenBy(x => x.ProductId)
                .ToList();
        }

        private static IEnumerable<CompatibilityMessageDto> BuildMissingMessages(
            IReadOnlyCollection<PcBuildCheckItemDto> items,
            IReadOnlyDictionary<int, ProductModel> productsById)
        {
            foreach (var item in items)
            {
                if (!productsById.ContainsKey(item.ProductId))
                {
                    yield return new CompatibilityMessageDto
                    {
                        Level = "error",
                        Message = $"Không tìm thấy sản phẩm ID = {item.ProductId} trong hệ thống."
                    };
                }
            }
        }

        private static ProductModel? MatchImportedProduct(
            PcBuildWorkbookRowModel row,
            IReadOnlyDictionary<int, ProductModel> productsById,
            IReadOnlyDictionary<string, List<ProductModel>> productsByName,
            List<string> warnings)
        {
            if (row.ProductId.HasValue &&
                row.ProductId.Value > 0 &&
                productsById.TryGetValue(row.ProductId.Value, out var byId))
            {
                return byId;
            }

            if (string.IsNullOrWhiteSpace(row.ProductName))
            {
                return null;
            }

            if (!productsByName.TryGetValue(row.ProductName.Trim(), out var candidates) || candidates.Count == 0)
            {
                return null;
            }

            if (row.ComponentType.HasValue && row.ComponentType.Value != PcComponentType.None)
            {
                var matchedByType = candidates.FirstOrDefault(x => x.ComponentType == row.ComponentType.Value);
                if (matchedByType != null)
                {
                    return matchedByType;
                }
            }

            if (candidates.Count > 1)
            {
                warnings.Add($"Sản phẩm \"{row.ProductName}\" có nhiều kết quả khớp, hệ thống lấy kết quả đầu tiên.");
            }

            return candidates[0];
        }

        private static string BuildMissingRowWarning(PcBuildWorkbookRowModel row)
        {
            if (row.ProductId.HasValue && row.ProductId.Value > 0)
            {
                return $"Không tìm thấy sản phẩm ID {row.ProductId.Value} trong dự án.";
            }

            if (!string.IsNullOrWhiteSpace(row.ProductName))
            {
                return $"Không tìm thấy sản phẩm \"{row.ProductName}\" trong dự án.";
            }

            return "Có một dòng trong file Excel không xác định được sản phẩm.";
        }

        private static bool IsMultiSlot(PcComponentType componentType)
        {
            return MultiSlotTypes.Contains(componentType);
        }

        private static string ResolveBuildName(string? buildName)
        {
            return string.IsNullOrWhiteSpace(buildName)
                ? "PC Build mới"
                : buildName.Trim();
        }

        private static int GetComponentOrder(PcComponentType componentType)
        {
            return componentType switch
            {
                PcComponentType.CPU => 1,
                PcComponentType.Mainboard => 2,
                PcComponentType.RAM => 3,
                PcComponentType.GPU => 4,
                PcComponentType.SSD => 5,
                PcComponentType.PSU => 6,
                PcComponentType.Cooler => 7,
                PcComponentType.Case => 8,
                PcComponentType.Monitor => 9,
                PcComponentType.HDD => 10,
                _ => 99
            };
        }
    }
}
