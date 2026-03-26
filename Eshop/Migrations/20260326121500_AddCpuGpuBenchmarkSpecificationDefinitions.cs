using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eshop.Migrations
{
    /// <inheritdoc />
    public partial class AddCpuGpuBenchmarkSpecificationDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SpecificationDefinitions",
                columns: new[] { "Id", "Code", "ComponentType", "DataType", "IsFilterable", "IsRequired", "Name", "SortOrder", "Unit" },
                values: new object[,]
                {
                    { 36, "cpu_benchmark_score", 1, 2, true, false, "Điểm benchmark CPU", 5, null },
                    { 37, "gpu_benchmark_score", 6, 2, true, false, "Điểm benchmark GPU", 6, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SpecificationDefinitions",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "SpecificationDefinitions",
                keyColumn: "Id",
                keyValue: 37);
        }
    }
}
