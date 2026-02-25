using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eshop.Migrations
{
    /// <inheritdoc />
    public partial class updateShip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CityCode",
                table: "Shippings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistrictCode",
                table: "Shippings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WardCode",
                table: "Shippings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CityCode",
                table: "Shippings");

            migrationBuilder.DropColumn(
                name: "DistrictCode",
                table: "Shippings");

            migrationBuilder.DropColumn(
                name: "WardCode",
                table: "Shippings");
        }
    }
}
