using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Helpers;
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
            var errors = rows
                .SelectMany(x => x.ValidationErrors ?? Enumerable.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var normalizedItems = new List<PcBuildCheckItemDto>();
            var latestSingleSlotByType = new Dictionary<PcComponentType, (PcBuildCheckItemDto Item, int RowNumber)>();
            var validRows = rows
                .Where(x => x.ValidationErrors == null || x.ValidationErrors.Count == 0)
                .ToList();

            var rowsByName = validRows
                .Where(x => !string.IsNullOrWhiteSpace(x.ProductName))
                .GroupBy(x => x.ProductName!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

            var candidateIds = validRows
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
                .WhereVisibleOnStorefront(_context)
                .Where(x =>
                    candidateIds.Contains(x.Id) ||
                    candidateNames.Contains(x.Name))
                .ToListAsync();

            var productsById = products.ToDictionary(x => x.Id);
            var productsByName = products
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in validRows)
            {
                var matchedProduct = MatchImportedProduct(row, productsById, productsByName, errors);
                if (matchedProduct == null)
                {
                    continue;
                }

                var componentType = row.ComponentType
                    ?? matchedProduct.ComponentType
                    ?? PcComponentType.None;

                if (componentType == PcComponentType.None)
                {
                    errors.Add(BuildImportRowError(row, $"Không xác định được loại linh kiện cho sản phẩm \"{matchedProduct.Name}\"."));
                    continue;
                }

                if (row.ComponentType.HasValue &&
                    row.ComponentType.Value != PcComponentType.None &&
                    matchedProduct.ComponentType.HasValue &&
                    matchedProduct.ComponentType.Value != PcComponentType.None &&
                    row.ComponentType.Value != matchedProduct.ComponentType.Value)
                {
                    errors.Add(BuildImportRowError(
                        row,
                        $"Loại linh kiện \"{GetComponentLabel(row.ComponentType.Value)}\" không khớp với sản phẩm \"{matchedProduct.Name}\" ({GetComponentLabel(matchedProduct.ComponentType.Value)})."));
                    continue;
                }

                var item = new PcBuildCheckItemDto
                {
                    ComponentType = componentType,
                    ProductId = matchedProduct.Id,
                    Quantity = row.Quantity
                };

                if (IsMultiSlot(componentType))
                {
                    normalizedItems.Add(item);
                    continue;
                }

                if (row.Quantity != 1)
                {
                    errors.Add(BuildImportRowError(
                        row,
                        $"{GetComponentLabel(componentType)} thuộc nhóm single-slot nên số lượng phải bằng 1."));
                    continue;
                }

                if (latestSingleSlotByType.TryGetValue(componentType, out var existingSingleSlot))
                {
                    errors.Add(BuildImportRowError(
                        row,
                        $"{GetComponentLabel(componentType)} thuộc nhóm single-slot và đã xuất hiện ở dòng {existingSingleSlot.RowNumber}."));
                    continue;
                }

                latestSingleSlotByType[componentType] = (item, row.RowNumber);
            }

            if (errors.Any())
            {
                return new PcBuildImportResultDto
                {
                    BuildName = ResolveBuildName(buildName),
                    ImportedRowCount = 0,
                    Errors = errors
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    Warnings = warnings
                };
            }

            normalizedItems.AddRange(latestSingleSlotByType.Values.Select(x => x.Item));
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
                Errors = new List<string>(),
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
                    .WhereVisibleOnStorefront(_context)
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
            List<string> errors)
        {
            if (row.ProductId.HasValue && row.ProductId.Value > 0)
            {
                if (!productsById.TryGetValue(row.ProductId.Value, out var byId))
                {
                    errors.Add(BuildImportRowError(row, $"Không tìm thấy sản phẩm có Product ID {row.ProductId.Value} trong hệ thống."));
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(row.ProductName) &&
                    !string.Equals(byId.Name, row.ProductName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(BuildImportRowError(row, $"Product ID {row.ProductId.Value} không khớp với tên sản phẩm \"{row.ProductName}\"."));
                    return null;
                }

                return byId;
            }

            if (string.IsNullOrWhiteSpace(row.ProductName))
            {
                return null;
            }

            if (!productsByName.TryGetValue(row.ProductName.Trim(), out var candidates) || candidates.Count == 0)
            {
                errors.Add(BuildImportRowError(row, $"Không tìm thấy sản phẩm \"{row.ProductName}\" trong hệ thống."));
                return null;
            }

            if (row.ComponentType.HasValue && row.ComponentType.Value != PcComponentType.None)
            {
                var matchedByType = candidates
                    .Where(x => x.ComponentType == row.ComponentType.Value)
                    .ToList();

                if (matchedByType.Count == 1)
                {
                    return matchedByType[0];
                }

                if (matchedByType.Count > 1)
                {
                    errors.Add(BuildImportRowError(
                        row,
                        $"Tên sản phẩm \"{row.ProductName}\" khớp nhiều sản phẩm cùng loại {GetComponentLabel(row.ComponentType.Value)}. Hãy nhập Product ID chính xác."));
                    return null;
                }

                errors.Add(BuildImportRowError(
                    row,
                    $"Sản phẩm \"{row.ProductName}\" không thuộc loại {GetComponentLabel(row.ComponentType.Value)}."));
                return null;
            }

            if (candidates.Count > 1)
            {
                errors.Add(BuildImportRowError(
                    row,
                    $"Tên sản phẩm \"{row.ProductName}\" khớp nhiều sản phẩm. Hãy nhập thêm Product ID hoặc loại linh kiện."));
                return null;
            }

            return candidates[0];
        }

        private static bool IsMultiSlot(PcComponentType componentType)
        {
            return MultiSlotTypes.Contains(componentType);
        }

        private static string BuildImportRowError(PcBuildWorkbookRowModel row, string message)
        {
            return row.RowNumber > 0
                ? $"Dòng {row.RowNumber}: {message}"
                : message;
        }

        private static string ResolveBuildName(string? buildName)
        {
            return string.IsNullOrWhiteSpace(buildName)
                ? "PC Build mới"
                : buildName.Trim();
        }

        private static string GetComponentLabel(PcComponentType componentType)
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
                PcComponentType.Cooler => "Cooler",
                PcComponentType.Monitor => "Monitor",
                _ => componentType.ToString()
            };
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
