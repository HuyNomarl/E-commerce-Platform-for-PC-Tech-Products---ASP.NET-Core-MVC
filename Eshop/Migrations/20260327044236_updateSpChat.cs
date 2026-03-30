using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Eshop.Migrations
{
    /// <inheritdoc />
    public partial class updateSpChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminReply",
                table: "RatingModels",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdminReplyAt",
                table: "RatingModels",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminReplyByName",
                table: "RatingModels",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminReplyUserId",
                table: "RatingModels",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

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

            migrationBuilder.DropColumn(
                name: "AdminReply",
                table: "RatingModels");

            migrationBuilder.DropColumn(
                name: "AdminReplyAt",
                table: "RatingModels");

            migrationBuilder.DropColumn(
                name: "AdminReplyByName",
                table: "RatingModels");

            migrationBuilder.DropColumn(
                name: "AdminReplyUserId",
                table: "RatingModels");
        }
    }
}
