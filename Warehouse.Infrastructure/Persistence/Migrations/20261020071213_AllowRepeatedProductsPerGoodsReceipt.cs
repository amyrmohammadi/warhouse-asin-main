using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowRepeatedProductsPerGoodsReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_GoodsReceiptId_ProductId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_GoodsReceiptId_ProductId",
                schema: "Business",
                table: "GoodsReceiptItems",
                columns: new[] { "GoodsReceiptId", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_GoodsReceiptId_ProductId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_GoodsReceiptId_ProductId",
                schema: "Business",
                table: "GoodsReceiptItems",
                columns: new[] { "GoodsReceiptId", "ProductId" },
                unique: true);
        }
    }
}
