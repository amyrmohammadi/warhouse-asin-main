using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowDuplicateProductTitlesForImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_NormalizedTitle",
                schema: "Business",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_NormalizedTitle",
                schema: "Business",
                table: "Products",
                column: "NormalizedTitle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_NormalizedTitle",
                schema: "Business",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_NormalizedTitle",
                schema: "Business",
                table: "Products",
                column: "NormalizedTitle",
                unique: true);
        }
    }
}
