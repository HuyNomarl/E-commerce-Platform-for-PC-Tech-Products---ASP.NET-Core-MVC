using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eshop.Migrations
{
    /// <inheritdoc />
    public partial class addExcelBuildPc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PcBuildShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShareCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    PcBuildId = table.Column<int>(type: "int", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReceiverUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcBuildShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcBuildShares_AspNetUsers_ReceiverUserId",
                        column: x => x.ReceiverUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PcBuildShares_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PcBuildShares_PcBuilds_PcBuildId",
                        column: x => x.PcBuildId,
                        principalTable: "PcBuilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PcBuildShares_PcBuildId",
                table: "PcBuildShares",
                column: "PcBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_PcBuildShares_ReceiverUserId_CreatedAt",
                table: "PcBuildShares",
                columns: new[] { "ReceiverUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PcBuildShares_SenderUserId",
                table: "PcBuildShares",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PcBuildShares_ShareCode",
                table: "PcBuildShares",
                column: "ShareCode",
                unique: true,
                filter: "[ShareCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PcBuildShares");
        }
    }
}
