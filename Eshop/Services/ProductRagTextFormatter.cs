using Eshop.Models;
using Eshop.Models.Enums;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Eshop.Services
{
    public static class ProductRagTextFormatter
    {
        public static string BuildCatalogDocument(ProductModel product, int availableStock)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Ho so san pham catalog de tu van build PC.");
            sb.AppendLine($"Ten san pham: {product.Name}");
            sb.AppendLine($"ProductId: {product.Id}");
            sb.AppendLine($"Slug: {product.Slug}");
            sb.AppendLine($"Loai san pham: {DescribeProductType(product.ProductType)}");
            sb.AppendLine($"Loai linh kien: {DescribeComponentType(product.ComponentType)}");
            sb.AppendLine($"Thuong hieu: {product.Publisher?.Name}");
            sb.AppendLine($"Danh muc: {product.Category?.Name}");
            sb.AppendLine($"Gia hien tai: {FormatCurrency(product.Price)}");
            sb.AppendLine($"Tinh trang storefront: {(product.Status == 1 ? "Dang hien thi" : "Dang an")}");
            sb.AppendLine($"Ton kho kha dung tham khao: {Math.Max(0, availableStock)}");

            if (!string.IsNullOrWhiteSpace(product.Description))
            {
                sb.AppendLine($"Mo ta: {NormalizeInlineText(product.Description)}");
            }

            var summarySpecs = BuildSummarySpecs(product);
            if (summarySpecs.Any())
            {
                sb.AppendLine("Tom tat thong so noi bat:");
                foreach (var item in summarySpecs)
                {
                    sb.AppendLine($"- {item}");
                }
            }

            var detailedSpecs = BuildDetailedSpecs(product);
            if (detailedSpecs.Any())
            {
                sb.AppendLine("Thong so ky thuat day du:");
                foreach (var item in detailedSpecs)
                {
                    sb.AppendLine($"- {item}");
                }
            }

            var keywords = BuildSearchKeywords(product);
            if (keywords.Any())
            {
                sb.AppendLine("Tu khoa tra cuu:");
                foreach (var keyword in keywords)
                {
                    sb.AppendLine($"- {keyword}");
                }
            }

            return sb.ToString().Trim();
        }

        public static string BuildPromptSummary(ProductModel product, int quantity = 1)
        {
            var parts = new List<string>();

            parts.Add($"[{DescribeComponentType(product.ComponentType)}] {product.Name}");
            parts.Add($"Gia {FormatCurrency(product.Price)}");

            var summarySpecs = BuildSummarySpecs(product);
            if (summarySpecs.Any())
            {
                parts.Add(string.Join(", ", summarySpecs.Take(4)));
            }

            if (quantity > 1)
            {
                parts.Add($"So luong {quantity}");
            }

            return string.Join(" | ", parts);
        }

        public static List<string> BuildSummarySpecs(ProductModel product)
        {
            var specs = (product.Specifications ?? new List<ProductSpecificationModel>())
                .Where(x => x.SpecificationDefinition != null && !string.IsNullOrWhiteSpace(x.SpecificationDefinition.Code))
                .GroupBy(x => x.SpecificationDefinition!.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            var result = new List<string>();

            switch (product.ComponentType)
            {
                case PcComponentType.CPU:
                    AddText(result, specs, "cpu_socket", "Socket");
                    AddNumber(result, specs, "cpu_cores", "Core");
                    AddNumber(result, specs, "cpu_threads", "Thread");
                    AddNumber(result, specs, "cpu_benchmark_score", "Benchmark");
                    break;

                case PcComponentType.Mainboard:
                    AddText(result, specs, "mb_socket", "Socket");
                    AddText(result, specs, "mb_chipset", "Chipset");
                    AddText(result, specs, "mb_ram_type", "RAM");
                    break;

                case PcComponentType.RAM:
                    AddNumber(result, specs, "ram_capacity_gb", "Dung luong", "GB");
                    AddText(result, specs, "ram_type", "Loai");
                    AddNumber(result, specs, "ram_bus_mhz", "Bus", "MHz");
                    break;

                case PcComponentType.SSD:
                    AddText(result, specs, "ssd_storage_type", "Loai");
                    AddNumber(result, specs, "ssd_capacity_gb", "Dung luong", "GB");
                    AddText(result, specs, "ssd_interface", "Giao tiep");
                    break;

                case PcComponentType.GPU:
                    AddText(result, specs, "gpu_chip", "GPU");
                    AddNumber(result, specs, "gpu_vram_gb", "VRAM", "GB");
                    AddNumber(result, specs, "gpu_benchmark_score", "Benchmark");
                    break;

                case PcComponentType.PSU:
                    AddNumber(result, specs, "psu_watt", "Cong suat", "W");
                    AddText(result, specs, "psu_efficiency", "Chung nhan");
                    break;

                case PcComponentType.Case:
                    AddNumber(result, specs, "case_max_gpu_length_mm", "GPU toi da", "mm");
                    AddNumber(result, specs, "case_max_cooler_height_mm", "Tan toi da", "mm");
                    break;

                case PcComponentType.Cooler:
                    AddNumber(result, specs, "cooler_height_mm", "Chieu cao", "mm");
                    break;

                case PcComponentType.Monitor:
                    AddNumber(result, specs, "monitor_size_inch", "Kich thuoc", "inch");
                    AddText(result, specs, "monitor_resolution", "Do phan giai");
                    AddNumber(result, specs, "monitor_refresh_rate_hz", "Tan so", "Hz");
                    break;
            }

            return result;
        }

        public static List<string> BuildDetailedSpecs(ProductModel product)
        {
            return (product.Specifications ?? new List<ProductSpecificationModel>())
                .Where(x => x.SpecificationDefinition != null)
                .OrderBy(x => x.SpecificationDefinition!.SortOrder)
                .ThenBy(x => x.SpecificationDefinition!.Name)
                .Select(x => $"{x.SpecificationDefinition!.Name}: {FormatSpecificationValue(x)}")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        public static string FormatSpecificationValue(ProductSpecificationModel spec)
        {
            if (!string.IsNullOrWhiteSpace(spec.ValueText))
            {
                return spec.ValueText.Trim();
            }

            if (spec.ValueNumber.HasValue)
            {
                var formatted = spec.ValueNumber.Value % 1 == 0
                    ? spec.ValueNumber.Value.ToString("0", CultureInfo.InvariantCulture)
                    : spec.ValueNumber.Value.ToString("0.##", CultureInfo.InvariantCulture);

                return string.IsNullOrWhiteSpace(spec.SpecificationDefinition?.Unit)
                    ? formatted
                    : $"{formatted} {spec.SpecificationDefinition.Unit}";
            }

            if (spec.ValueBool.HasValue)
            {
                return spec.ValueBool.Value ? "Co" : "Khong";
            }

            if (!string.IsNullOrWhiteSpace(spec.ValueJson))
            {
                try
                {
                    var values = JsonSerializer.Deserialize<List<string>>(spec.ValueJson) ?? new List<string>();
                    return string.Join(", ", values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
                }
                catch
                {
                    return spec.ValueJson;
                }
            }

            return string.Empty;
        }

        public static List<string> BuildSearchKeywords(ProductModel product)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddKeyword(keywords, product.Name);
            AddKeyword(keywords, product.Publisher?.Name);
            AddKeyword(keywords, product.Category?.Name);
            AddKeyword(keywords, DescribeProductType(product.ProductType));
            AddKeyword(keywords, DescribeComponentType(product.ComponentType));

            foreach (var spec in product.Specifications ?? new List<ProductSpecificationModel>())
            {
                if (spec.SpecificationDefinition == null)
                {
                    continue;
                }

                AddKeyword(keywords, spec.SpecificationDefinition.Name);
                AddKeyword(keywords, FormatSpecificationValue(spec));
            }

            return keywords
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x)
                .ToList();
        }

        private static void AddText(
            List<string> result,
            Dictionary<string, ProductSpecificationModel> specs,
            string code,
            string label)
        {
            if (specs.TryGetValue(code, out var spec) && !string.IsNullOrWhiteSpace(spec.ValueText))
            {
                result.Add($"{label}: {spec.ValueText.Trim()}");
            }
        }

        private static void AddNumber(
            List<string> result,
            Dictionary<string, ProductSpecificationModel> specs,
            string code,
            string label,
            string? unit = null)
        {
            if (specs.TryGetValue(code, out var spec) && spec.ValueNumber.HasValue)
            {
                var formatted = spec.ValueNumber.Value % 1 == 0
                    ? spec.ValueNumber.Value.ToString("0", CultureInfo.InvariantCulture)
                    : spec.ValueNumber.Value.ToString("0.##", CultureInfo.InvariantCulture);

                result.Add($"{label}: {formatted}{(string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}")}");
            }
        }

        private static string DescribeProductType(ProductType productType)
        {
            return productType switch
            {
                ProductType.Component => "Linh kien PC",
                ProductType.Monitor => "Man hinh",
                ProductType.PcPrebuilt => "PC dung san",
                _ => "San pham thuong",
            };
        }

        private static string DescribeComponentType(PcComponentType? componentType)
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
                _ => "Chua phan loai",
            };
        }

        private static string FormatCurrency(decimal value)
        {
            return string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:#,##0} VND", value);
        }

        private static string NormalizeInlineText(string text)
        {
            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static void AddKeyword(HashSet<string> keywords, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = value.Trim();
            if (normalized.Length <= 1)
            {
                return;
            }

            keywords.Add(normalized);
        }
    }
}
