using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eshop.Migrations
{
    [Migration("20260326080000_EnhanceProductReviewsAndCompare")]
    public partial class EnhanceProductReviewsAndCompare : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Compares",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Compares",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RatingModels",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RatingMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RatingId = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingMedia_RatingModels_RatingId",
                        column: x => x.RatingId,
                        principalTable: "RatingModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
DELETE FROM Compares
WHERE UserId IS NULL OR LTRIM(RTRIM(UserId)) = '';

;WITH RankedCompares AS
(
    SELECT Id,
           ROW_NUMBER() OVER (PARTITION BY UserId, ProductId ORDER BY CreatedAt DESC, Id DESC) AS RowNum
    FROM Compares
)
DELETE FROM RankedCompares
WHERE RowNum > 1;
");

            migrationBuilder.Sql(@"
;WITH RankedRatings AS
(
    SELECT Id,
           ROW_NUMBER() OVER (PARTITION BY UserId, ProductId ORDER BY CreatedAt DESC, Id DESC) AS RowNum
    FROM RatingModels
)
DELETE FROM RankedRatings
WHERE RowNum > 1;
");

            migrationBuilder.CreateIndex(
                name: "IX_Compares_UserId_ProductId",
                table: "Compares",
                columns: new[] { "UserId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatingMedia_RatingId_SortOrder",
                table: "RatingMedia",
                columns: new[] { "RatingId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RatingModels_UserId_ProductId",
                table: "RatingModels",
                columns: new[] { "UserId", "ProductId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RatingMedia");

            migrationBuilder.DropIndex(
                name: "IX_Compares_UserId_ProductId",
                table: "Compares");

            migrationBuilder.DropIndex(
                name: "IX_RatingModels_UserId_ProductId",
                table: "RatingModels");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Compares");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RatingModels");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Compares",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);
        }
    }
}
