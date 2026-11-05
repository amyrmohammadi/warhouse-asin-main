using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndexWarehouseStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_WarehouseId",
                schema: "Business",
                table: "StockMovements");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WarehouseId_ProductId_MovementType",
                schema: "Business",
                table: "StockMovements",
                columns: new[] { "WarehouseId", "ProductId", "MovementType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_WarehouseId_ProductId_MovementType",
                schema: "Business",
                table: "StockMovements");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WarehouseId",
                schema: "Business",
                table: "StockMovements",
                column: "WarehouseId");
        }
    }
}
