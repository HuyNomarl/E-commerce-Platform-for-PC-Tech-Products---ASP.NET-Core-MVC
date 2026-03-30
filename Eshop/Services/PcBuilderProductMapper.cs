using Eshop.Models;
using Eshop.Models.Enums;
using Eshop.Models.ViewModels;
using Eshop.Helpers;
using System.Text.Json;

namespace Eshop.Services
{
    public static class PcBuilderProductMapper
    {
        public static PcBuilderProductCardDto ToCardDto(ProductModel product)
        {
            return new PcBuilderProductCardDto
            {
                Id = product.Id,
                Name = product.Name,
                Image = ProductImageHelper.ResolveProductImage(product),
                Price = product.Price,
                PublisherName = product.Publisher?.Name,
                PublisherId = product.PublisherId,
                Stock = product.Quantity,
                ComponentType = product.ComponentType?.ToString() ?? string.Empty,
                SummarySpecs = BuildSummarySpecs(product),
                FilterSpecs = BuildFilterSpecs(product)
            };
        }

        private static List<string> BuildSummarySpecs(ProductModel product)
        {
            var specs = (product.Specifications ?? new List<ProductSpecificationModel>())
                .Where(x => x.SpecificationDefinition != null && !string.IsNullOrWhiteSpace(x.SpecificationDefinition.Code))
                .GroupBy(x => x.SpecificationDefinition.Code)
                .ToDictionary(x => x.Key, x => x.First());

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
                result.Add($"{label}: {spec.ValueNumber}{(string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}")}");
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

        private static string? GetSpecJson(ProductModel product, string code)
        {
            return product.Specifications
                .FirstOrDefault(x => x.SpecificationDefinition.Code == code)
                ?.ValueJson;
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
                    Value = $"{FormatNumber(value.Value)}{(string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}")}"
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

        private static void AddJsonArrayFilterSpec(List<PcBuilderProductSpecDto> result, ProductModel product, string code, string label)
        {
            var raw = GetSpecJson(product, code);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

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
