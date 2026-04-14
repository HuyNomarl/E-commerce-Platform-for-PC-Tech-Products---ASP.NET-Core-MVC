using Eshop.Models;
using Eshop.Models.ViewModels;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Eshop.Helpers
{
    public static class ProductCatalogFilterHelper
    {
        private static readonly Regex AmdRyzenRegex = new(@"amd\s+ryzen\s+(3|5|7|9)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex IntelCoreRegex = new(@"intel\s+core\s+i(3|5|7|9)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex IntelModelRegex = new(@"\bi(3|5|7|9)\s*[- ]?\d{3,5}[a-z0-9]*\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RyzenModelRegex = new(@"ryzen\s+(3|5|7|9)\s*\d{3,5}[a-z0-9]*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NvidiaGpuRegex = new(@"\b(?:nvidia\s*)?(rtx\s*\d{4,5}(?:\s*ti)?(?:\s*\d+\s*gb)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AmdGpuRegex = new(@"\b(?:amd\s*)?(rx\s*\d{4,5}(?:\s*xt)?(?:\s*\d+\s*gb)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RamRegex = new(@"\b(8|12|16|24|32|48|64|96|128)\s*gb(?:\s*ddr\d)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string? ExtractCpuLabel(ProductModel product)
        {
            var candidates = new[]
            {
                GetSpecText(product, "cpu_name", "cpu_model", "cpu_series", "cpu_chip"),
                product.Name
            };

            foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var simplified = SimplifyCpuLabel(candidate!);
                if (!string.IsNullOrWhiteSpace(simplified))
                {
                    return simplified;
                }
            }

            return null;
        }

        public static string? ExtractRamLabel(ProductModel product)
        {
            var capacity = GetSpecNumber(product, "ram_capacity_gb");
            if (capacity.HasValue && capacity.Value > 0)
            {
                return $"{capacity.Value:0}GB";
            }

            var candidates = new[]
            {
                GetSpecText(product, "ram_capacity", "ram_type"),
                product.Name
            };

            foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var match = RamRegex.Match(candidate!);
                if (match.Success)
                {
                    return $"{match.Groups[1].Value}GB";
                }
            }

            return null;
        }

        public static string? ExtractGpuLabel(ProductModel product)
        {
            var gpuText = GetSpecText(product, "gpu_chip", "gpu_name", "gpu_model");
            var vram = GetSpecNumber(product, "gpu_vram_gb");

            var candidates = new[]
            {
                gpuText,
                product.Name
            };

            foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var simplified = SimplifyGpuLabel(candidate!, vram);
                if (!string.IsNullOrWhiteSpace(simplified))
                {
                    return simplified;
                }
            }

            return null;
        }

        public static string NormalizeToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            var ascii = builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .ToLowerInvariant()
                .Replace("đ", "d");

            ascii = Regex.Replace(ascii, @"[^a-z0-9]+", "-").Trim('-');
            return ascii;
        }

        public static List<ProductModel> ApplySorting(List<ProductModel> products, string? sortBy)
        {
            return sortBy switch
            {
                "price_asc" => products.OrderBy(x => x.Price).ThenByDescending(x => x.Id).ToList(),
                "price_desc" => products.OrderByDescending(x => x.Price).ThenByDescending(x => x.Id).ToList(),
                "name_asc" => products.OrderBy(x => x.Name).ThenByDescending(x => x.Id).ToList(),
                "name_desc" => products.OrderByDescending(x => x.Name).ThenByDescending(x => x.Id).ToList(),
                _ => products.OrderByDescending(x => x.Id).ToList()
            };
        }

        public static List<string> NormalizeSelections(IEnumerable<string>? values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Select(NormalizeToken)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<string> NormalizePriceSelections(IEnumerable<string>? values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();
        }

        public static string NormalizeSort(string? sortBy)
        {
            var normalized = (sortBy ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "price_asc" or "price_desc" or "name_asc" or "name_desc" => normalized,
                _ => "newest"
            };
        }

        public static bool MatchesSelection(string? rawValue, List<string> selectedValues)
        {
            if (!selectedValues.Any())
            {
                return true;
            }

            var normalized = NormalizeToken(rawValue);
            return !string.IsNullOrWhiteSpace(normalized)
                && selectedValues.Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }

        public static bool MatchesPriceRanges(decimal price, List<string> selectedPriceRanges)
        {
            if (!selectedPriceRanges.Any())
            {
                return true;
            }

            return selectedPriceRanges.Any(range => IsPriceInRange(price, range));
        }

        public static List<ProductCatalogFilterOptionViewModel> BuildPriceRanges(
            List<ProductModel> products,
            List<string> selectedPriceRanges)
        {
            var ranges = new[]
            {
                (Value: "0-15000000", Label: "Dưới 15 triệu"),
                (Value: "15000000-25000000", Label: "15 triệu - 25 triệu"),
                (Value: "25000000-35000000", Label: "25 triệu - 35 triệu"),
                (Value: "35000000-45000000", Label: "35 triệu - 45 triệu"),
                (Value: "45000000-60000000", Label: "45 triệu - 60 triệu"),
                (Value: "60000000+", Label: "Trên 60 triệu")
            };

            return ranges
                .Select(range =>
                {
                    var count = products.Count(x => IsPriceInRange(x.Price, range.Value));
                    return new ProductCatalogFilterOptionViewModel
                    {
                        Value = range.Value,
                        Label = range.Label,
                        Count = count,
                        Selected = selectedPriceRanges.Contains(range.Value, StringComparer.OrdinalIgnoreCase)
                    };
                })
                .Where(x => x.Count > 0 || x.Selected)
                .ToList();
        }

        public static List<ProductCatalogFilterOptionViewModel> BuildFilterOptions(
            List<ProductModel> products,
            Func<ProductModel, string?> labelSelector,
            List<string> selectedValues)
        {
            return products
                .Select(labelSelector)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .GroupBy(x => NormalizeToken(x), StringComparer.OrdinalIgnoreCase)
                .Select(group => new ProductCatalogFilterOptionViewModel
                {
                    Value = group.Key,
                    Label = group
                        .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(x => x.Count())
                        .ThenBy(x => x.Key)
                        .Select(x => x.Key)
                        .First(),
                    Count = group.Count(),
                    Selected = selectedValues.Contains(group.Key, StringComparer.OrdinalIgnoreCase)
                })
                .OrderBy(x => x.Label)
                .ToList();
        }

        public static bool IsPriceInRange(decimal price, string rangeValue)
        {
            if (string.IsNullOrWhiteSpace(rangeValue))
            {
                return false;
            }

            if (rangeValue.EndsWith("+", StringComparison.Ordinal))
            {
                var lowerText = rangeValue[..^1];
                return decimal.TryParse(lowerText, out var lowerBound) && price >= lowerBound;
            }

            var parts = rangeValue.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            return decimal.TryParse(parts[0], out var min)
                && decimal.TryParse(parts[1], out var max)
                && price >= min
                && price < max;
        }

        private static string? SimplifyCpuLabel(string value)
        {
            var compact = NormalizeSpacing(value);
            if (string.IsNullOrWhiteSpace(compact))
            {
                return null;
            }

            var amdMatch = AmdRyzenRegex.Match(compact);
            if (amdMatch.Success)
            {
                return $"AMD Ryzen {amdMatch.Groups[1].Value}";
            }

            var intelMatch = IntelCoreRegex.Match(compact);
            if (intelMatch.Success)
            {
                return $"Intel Core i{intelMatch.Groups[1].Value}";
            }

            var intelModelMatch = IntelModelRegex.Match(compact);
            if (intelModelMatch.Success)
            {
                return $"Intel Core i{intelModelMatch.Groups[1].Value}";
            }

            var ryzenModelMatch = RyzenModelRegex.Match(compact);
            if (ryzenModelMatch.Success)
            {
                return $"AMD Ryzen {ryzenModelMatch.Groups[1].Value}";
            }

            if (compact.Contains("xeon", StringComparison.OrdinalIgnoreCase))
            {
                return "Intel Xeon";
            }

            return compact.Length <= 40 ? compact : compact[..40].Trim() + "...";
        }

        private static string? SimplifyGpuLabel(string value, decimal? vram)
        {
            var compact = NormalizeSpacing(value);
            if (string.IsNullOrWhiteSpace(compact))
            {
                return null;
            }

            var nvidiaMatch = NvidiaGpuRegex.Match(compact);
            if (nvidiaMatch.Success)
            {
                return BuildGpuLabel("NVIDIA", nvidiaMatch.Groups[1].Value, vram);
            }

            var amdMatch = AmdGpuRegex.Match(compact);
            if (amdMatch.Success)
            {
                return BuildGpuLabel("AMD", amdMatch.Groups[1].Value, vram);
            }

            return compact.Length <= 48 ? compact : compact[..48].Trim() + "...";
        }

        private static string BuildGpuLabel(string vendor, string chip, decimal? vram)
        {
            var cleanedChip = NormalizeChipName(chip);
            if (vram.HasValue && vram.Value > 0 &&
                !cleanedChip.Contains("GB", StringComparison.OrdinalIgnoreCase))
            {
                cleanedChip += $" {vram.Value:0}GB";
            }

            return $"{vendor} {cleanedChip}".Trim();
        }

        private static string NormalizeChipName(string value)
        {
            var tokens = NormalizeSpacing(value)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(token =>
                {
                    if (token.Equals("rtx", StringComparison.OrdinalIgnoreCase)) return "RTX";
                    if (token.Equals("rx", StringComparison.OrdinalIgnoreCase)) return "RX";
                    if (token.Equals("ti", StringComparison.OrdinalIgnoreCase)) return "Ti";
                    if (token.EndsWith("gb", StringComparison.OrdinalIgnoreCase))
                    {
                        return token[..^2] + "GB";
                    }

                    return token.ToUpperInvariant();
                });

            return string.Join(" ", tokens);
        }

        private static string NormalizeSpacing(string value)
        {
            return Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        }

        private static string? GetSpecText(ProductModel product, params string[] codes)
        {
            foreach (var code in codes)
            {
                var spec = product.Specifications?
                    .FirstOrDefault(x => string.Equals(x.SpecificationDefinition?.Code, code, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(spec?.ValueText))
                {
                    return spec.ValueText.Trim();
                }
            }

            return null;
        }

        private static decimal? GetSpecNumber(ProductModel product, params string[] codes)
        {
            foreach (var code in codes)
            {
                var spec = product.Specifications?
                    .FirstOrDefault(x => string.Equals(x.SpecificationDefinition?.Code, code, StringComparison.OrdinalIgnoreCase));

                if (spec?.ValueNumber.HasValue == true)
                {
                    return spec.ValueNumber.Value;
                }
            }

            return null;
        }
    }
}
