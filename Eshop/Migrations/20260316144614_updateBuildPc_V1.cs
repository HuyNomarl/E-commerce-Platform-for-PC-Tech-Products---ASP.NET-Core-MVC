using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Eshop.Migrations
{
    /// <inheritdoc />
    public partial class updateBuildPc_V1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductComponentModel");

            migrationBuilder.AlterColumn<int>(
                name: "ProductType",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComponentType",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PcBuilds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BuildName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcBuilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrebuiltPcComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComponentType = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ComponentProductId = table.Column<int>(type: "int", nullable: false),
                    AdditionalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrebuiltPcComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrebuiltPcComponents_Products_ComponentProductId",
                        column: x => x.ComponentProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrebuiltPcComponents_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpecificationDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComponentType = table.Column<int>(type: "int", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsFilterable = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecificationDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PcBuildItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PcBuildId = table.Column<int>(type: "int", nullable: false),
                    ComponentType = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcBuildItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcBuildItems_PcBuilds_PcBuildId",
                        column: x => x.PcBuildId,
                        principalTable: "PcBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PcBuildItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductSpecifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SpecificationDefinitionId = table.Column<int>(type: "int", nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueNumber = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValueBool = table.Column<bool>(type: "bit", nullable: true),
                    ValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSpecifications_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductSpecifications_SpecificationDefinitions_SpecificationDefinitionId",
                        column: x => x.SpecificationDefinitionId,
                        principalTable: "SpecificationDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SpecificationDefinitions",
                columns: new[] { "Id", "Code", "ComponentType", "DataType", "IsFilterable", "IsRequired", "Name", "SortOrder", "Unit" },
                values: new object[,]
                {
                    { 1, "cpu_socket", 1, 1, true, true, "Socket CPU", 1, null },
                    { 2, "cpu_cores", 1, 2, true, false, "Số nhân", 2, null },
                    { 3, "cpu_threads", 1, 2, true, false, "Số luồng", 3, null },
                    { 4, "cpu_tdp_w", 1, 2, true, false, "TDP", 4, "W" },
                    { 5, "mb_socket", 2, 1, true, true, "Socket Mainboard", 1, null },
                    { 6, "mb_chipset", 2, 1, true, false, "Chipset", 2, null },
                    { 7, "mb_form_factor", 2, 1, true, true, "Kích thước Mainboard", 3, null },
                    { 8, "mb_ram_type", 2, 1, true, true, "Loại RAM hỗ trợ", 4, null },
                    { 9, "mb_ram_slots", 2, 2, true, false, "Số khe RAM", 5, null },
                    { 10, "mb_max_ram_gb", 2, 2, true, false, "RAM tối đa", 6, "GB" },
                    { 11, "ram_type", 3, 1, true, true, "Loại RAM", 1, null },
                    { 12, "ram_capacity_gb", 3, 2, true, false, "Dung lượng RAM", 2, "GB" },
                    { 13, "ram_bus_mhz", 3, 2, true, false, "Bus RAM", 3, "MHz" },
                    { 14, "ram_kit_modules", 3, 2, true, false, "Số thanh / kit", 4, null },
                    { 15, "ssd_storage_type", 4, 1, true, false, "Loại ổ", 1, null },
                    { 16, "ssd_capacity_gb", 4, 2, true, false, "Dung lượng SSD", 2, "GB" },
                    { 17, "ssd_interface", 4, 1, true, false, "Chuẩn giao tiếp", 3, null },
                    { 18, "gpu_chip", 6, 1, true, false, "GPU", 1, null },
                    { 19, "gpu_vram_gb", 6, 2, true, false, "VRAM", 2, "GB" },
                    { 20, "gpu_length_mm", 6, 2, true, false, "Chiều dài VGA", 3, "mm" },
                    { 21, "gpu_tdp_w", 6, 2, true, false, "Công suất VGA", 4, "W" },
                    { 22, "gpu_recommended_psu_w", 6, 2, true, false, "PSU đề nghị", 5, "W" },
                    { 23, "psu_watt", 7, 2, true, true, "Công suất PSU", 1, "W" },
                    { 24, "psu_efficiency", 7, 1, true, false, "Chứng nhận", 2, null },
                    { 25, "psu_standard", 7, 1, true, false, "Chuẩn nguồn", 3, null },
                    { 26, "case_supported_mb_sizes", 8, 4, true, false, "Mainboard hỗ trợ", 1, null },
                    { 27, "case_max_gpu_length_mm", 8, 2, true, false, "GPU dài tối đa", 2, "mm" },
                    { 28, "case_max_cooler_height_mm", 8, 2, true, false, "Tản nhiệt cao tối đa", 3, "mm" },
                    { 29, "case_psu_standard", 8, 1, true, false, "Chuẩn PSU hỗ trợ", 4, null },
                    { 30, "cooler_height_mm", 9, 2, true, false, "Chiều cao tản nhiệt", 1, "mm" },
                    { 31, "monitor_size_inch", 10, 2, true, false, "Kích thước màn hình", 1, "inch" },
                    { 32, "monitor_resolution", 10, 1, true, false, "Độ phân giải", 2, null },
                    { 33, "monitor_refresh_rate_hz", 10, 2, true, false, "Tần số quét", 3, "Hz" },
                    { 34, "monitor_brightness_nits", 10, 2, true, false, "Độ sáng", 4, "nits" },
                    { 35, "monitor_panel_type", 10, 1, true, false, "Tấm nền", 5, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PcBuildItems_PcBuildId",
                table: "PcBuildItems",
                column: "PcBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_PcBuildItems_ProductId",
                table: "PcBuildItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PrebuiltPcComponents_ComponentProductId",
                table: "PrebuiltPcComponents",
                column: "ComponentProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PrebuiltPcComponents_ProductId",
                table: "PrebuiltPcComponents",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecifications_ProductId",
                table: "ProductSpecifications",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecifications_SpecificationDefinitionId",
                table: "ProductSpecifications",
                column: "SpecificationDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationDefinitions_Code",
                table: "SpecificationDefinitions",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PcBuildItems");

            migrationBuilder.DropTable(
                name: "PrebuiltPcComponents");

            migrationBuilder.DropTable(
                name: "ProductSpecifications");

            migrationBuilder.DropTable(
                name: "PcBuilds");

            migrationBuilder.DropTable(
                name: "SpecificationDefinitions");

            migrationBuilder.DropColumn(
                name: "ComponentType",
                table: "Products");

            migrationBuilder.AlterColumn<string>(
                name: "ProductType",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "ProductComponentModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ComponentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComponentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductComponentModel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductComponentModel_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductComponentModel_ProductId",
                table: "ProductComponentModel",
                column: "ProductId");
        }
    }
}
