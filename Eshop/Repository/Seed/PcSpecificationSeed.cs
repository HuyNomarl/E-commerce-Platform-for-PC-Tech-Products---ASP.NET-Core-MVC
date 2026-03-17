using Eshop.Models;
using Eshop.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Repository.Seed
{
    public static class PcSpecificationSeed
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SpecificationDefinitionModel>().HasData(
                // CPU
                new SpecificationDefinitionModel { Id = 1, Code = "cpu_socket", Name = "Socket CPU", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.CPU, IsRequired = true, SortOrder = 1 },
                new SpecificationDefinitionModel { Id = 2, Code = "cpu_cores", Name = "Số nhân", DataType = SpecificationDataType.Number, ComponentType = PcComponentType.CPU, SortOrder = 2 },
                new SpecificationDefinitionModel { Id = 3, Code = "cpu_threads", Name = "Số luồng", DataType = SpecificationDataType.Number, ComponentType = PcComponentType.CPU, SortOrder = 3 },
                new SpecificationDefinitionModel { Id = 4, Code = "cpu_tdp_w", Name = "TDP", DataType = SpecificationDataType.Number, Unit = "W", ComponentType = PcComponentType.CPU, SortOrder = 4 },

                // Mainboard
                new SpecificationDefinitionModel { Id = 5, Code = "mb_socket", Name = "Socket Mainboard", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.Mainboard, IsRequired = true, SortOrder = 1 },
                new SpecificationDefinitionModel { Id = 6, Code = "mb_chipset", Name = "Chipset", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.Mainboard, SortOrder = 2 },
                new SpecificationDefinitionModel { Id = 7, Code = "mb_form_factor", Name = "Kích thước Mainboard", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.Mainboard, IsRequired = true, SortOrder = 3 },
                new SpecificationDefinitionModel { Id = 8, Code = "mb_ram_type", Name = "Loại RAM hỗ trợ", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.Mainboard, IsRequired = true, SortOrder = 4 },
                new SpecificationDefinitionModel { Id = 9, Code = "mb_ram_slots", Name = "Số khe RAM", DataType = SpecificationDataType.Number, ComponentType = PcComponentType.Mainboard, SortOrder = 5 },
                new SpecificationDefinitionModel { Id = 10, Code = "mb_max_ram_gb", Name = "RAM tối đa", DataType = SpecificationDataType.Number, Unit = "GB", ComponentType = PcComponentType.Mainboard, SortOrder = 6 },

                // RAM
                new SpecificationDefinitionModel { Id = 11, Code = "ram_type", Name = "Loại RAM", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.RAM, IsRequired = true, SortOrder = 1 },
                new SpecificationDefinitionModel { Id = 12, Code = "ram_capacity_gb", Name = "Dung lượng RAM", DataType = SpecificationDataType.Number, Unit = "GB", ComponentType = PcComponentType.RAM, SortOrder = 2 },
                new SpecificationDefinitionModel { Id = 13, Code = "ram_bus_mhz", Name = "Bus RAM", DataType = SpecificationDataType.Number, Unit = "MHz", ComponentType = PcComponentType.RAM, SortOrder = 3 },
                new SpecificationDefinitionModel { Id = 14, Code = "ram_kit_modules", Name = "Số thanh / kit", DataType = SpecificationDataType.Number, ComponentType = PcComponentType.RAM, SortOrder = 4 },

                // SSD
                new SpecificationDefinitionModel { Id = 15, Code = "ssd_storage_type", Name = "Loại ổ", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.SSD, SortOrder = 1 },
                new SpecificationDefinitionModel { Id = 16, Code = "ssd_capacity_gb", Name = "Dung lượng SSD", DataType = SpecificationDataType.Number, Unit = "GB", ComponentType = PcComponentType.SSD, SortOrder = 2 },
                new SpecificationDefinitionModel { Id = 17, Code = "ssd_interface", Name = "Chuẩn giao tiếp", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.SSD, SortOrder = 3 },

                // GPU
                new SpecificationDefinitionModel { Id = 18, Code = "gpu_chip", Name = "GPU", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.GPU, SortOrder = 1 },
                new SpecificationDefinitionModel { Id = 19, Code = "gpu_vram_gb", Name = "VRAM", DataType = SpecificationDataType.Number, Unit = "GB", ComponentType = PcComponentType.GPU, SortOrder = 2 },
                new SpecificationDefinitionModel { Id = 20, Code = "gpu_length_mm", Name = "Chiều dài VGA", DataType = SpecificationDataType.Number, Unit = "mm", ComponentType = PcComponentType.GPU, SortOrder = 3 },
                new SpecificationDefinitionModel { Id = 21, Code = "gpu_tdp_w", Name = "Công suất VGA", DataType = SpecificationDataType.Number, Unit = "W", ComponentType = PcComponentType.GPU, SortOrder = 4 },
                new SpecificationDefinitionModel { Id = 22, Code = "gpu_recommended_psu_w", Name = "PSU đề nghị", DataType = SpecificationDataType.Number, Unit = "W", ComponentType = PcComponentType.GPU, SortOrder = 5 },

                // PSU
                new SpecificationDefinitionModel { Id = 23, Code = "psu_watt", Name = "Công suất PSU", DataType = SpecificationDataType.Number, Unit = "W", ComponentType = PcComponentType.PSU, IsRequired = true, SortOrder = 1 },
                new SpecificationDefinitionModel { Id = 24, Code = "psu_efficiency", Name = "Chứng nhận", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.PSU, SortOrder = 2 },
                new SpecificationDefinitionModel { Id = 25, Code = "psu_standard", Name = "Chuẩn nguồn", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.PSU, SortOrder = 3 },

                // Case
                new SpecificationDefinitionModel { Id = 26, Code = "case_supported_mb_sizes", Name = "Mainboard hỗ trợ", DataType = SpecificationDataType.Json, ComponentType = PcComponentType.Case, SortOrder = 1 },
                new SpecificationDefinitionModel { Id = 27, Code = "case_max_gpu_length_mm", Name = "GPU dài tối đa", DataType = SpecificationDataType.Number, Unit = "mm", ComponentType = PcComponentType.Case, SortOrder = 2 },
                new SpecificationDefinitionModel { Id = 28, Code = "case_max_cooler_height_mm", Name = "Tản nhiệt cao tối đa", DataType = SpecificationDataType.Number, Unit = "mm", ComponentType = PcComponentType.Case, SortOrder = 3 },
                new SpecificationDefinitionModel { Id = 29, Code = "case_psu_standard", Name = "Chuẩn PSU hỗ trợ", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.Case, SortOrder = 4 },

                // Cooler
                new SpecificationDefinitionModel { Id = 30, Code = "cooler_height_mm", Name = "Chiều cao tản nhiệt", DataType = SpecificationDataType.Number, Unit = "mm", ComponentType = PcComponentType.Cooler, SortOrder = 1 },

                // Monitor
                new SpecificationDefinitionModel { Id = 31, Code = "monitor_size_inch", Name = "Kích thước màn hình", DataType = SpecificationDataType.Number, Unit = "inch", ComponentType = PcComponentType.Monitor, SortOrder = 1 },
                new SpecificationDefinitionModel { Id = 32, Code = "monitor_resolution", Name = "Độ phân giải", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.Monitor, SortOrder = 2 },
                new SpecificationDefinitionModel { Id = 33, Code = "monitor_refresh_rate_hz", Name = "Tần số quét", DataType = SpecificationDataType.Number, Unit = "Hz", ComponentType = PcComponentType.Monitor, SortOrder = 3 },
                new SpecificationDefinitionModel { Id = 34, Code = "monitor_brightness_nits", Name = "Độ sáng", DataType = SpecificationDataType.Number, Unit = "nits", ComponentType = PcComponentType.Monitor, SortOrder = 4 },
                new SpecificationDefinitionModel { Id = 35, Code = "monitor_panel_type", Name = "Tấm nền", DataType = SpecificationDataType.Text, ComponentType = PcComponentType.Monitor, SortOrder = 5 }
            );
        }
    }
}