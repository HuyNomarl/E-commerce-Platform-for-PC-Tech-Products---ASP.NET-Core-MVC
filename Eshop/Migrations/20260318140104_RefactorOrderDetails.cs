using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eshop.Migrations
{
    /// <inheritdoc />
    public partial class RefactorOrderDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "OrderDetails",
                newName: "ProductName");

            migrationBuilder.RenameColumn(
                name: "OrderCode",
                table: "OrderDetails",
                newName: "ProductImage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProductName",
                table: "OrderDetails",
                newName: "UserName");

            migrationBuilder.RenameColumn(
                name: "ProductImage",
                table: "OrderDetails",
                newName: "OrderCode");
        }
    }
}
