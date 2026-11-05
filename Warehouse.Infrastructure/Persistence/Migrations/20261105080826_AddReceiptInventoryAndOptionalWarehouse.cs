using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptInventoryAndOptionalWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                schema: "Business",
                table: "GoodsIssueItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockMovements",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReversedMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovements_Nonzero", "[QuantityDelta] <> 0");
                    table.ForeignKey(
                        name: "FK_StockMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "Business",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockMovements_ReversedMovementId",
                        column: x => x.ReversedMovementId,
                        principalSchema: "Business",
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "Business",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseStockBalances",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseStockBalances", x => x.Id);
                    table.CheckConstraint("CK_WarehouseStockBalances_Nonnegative", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_WarehouseStockBalances_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "Business",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseStockBalances_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "Business",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueItems_WarehouseId",
                schema: "Business",
                table: "GoodsIssueItems",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OperationId",
                schema: "Business",
                table: "StockMovements",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId",
                schema: "Business",
                table: "StockMovements",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReversedMovementId",
                schema: "Business",
                table: "StockMovements",
                column: "ReversedMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_SourceDocumentId_MovementType_SourceLineId",
                schema: "Business",
                table: "StockMovements",
                columns: new[] { "SourceDocumentId", "MovementType", "SourceLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WarehouseId",
                schema: "Business",
                table: "StockMovements",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseStockBalances_ProductId",
                schema: "Business",
                table: "WarehouseStockBalances",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseStockBalances_WarehouseId_ProductId",
                schema: "Business",
                table: "WarehouseStockBalances",
                columns: new[] { "WarehouseId", "ProductId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsIssueItems_Warehouses_WarehouseId",
                schema: "Business",
                table: "GoodsIssueItems",
                column: "WarehouseId",
                principalSchema: "Business",
                principalTable: "Warehouses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Business].[GoodsReceiptItems] WHERE [WarehouseId] IS NULL)
                    THROW 50001, N'Cannot restore a required warehouse while unassigned receipt rows exist.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsIssueItems_Warehouses_WarehouseId",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.DropTable(
                name: "StockMovements",
                schema: "Business");

            migrationBuilder.DropTable(
                name: "WarehouseStockBalances",
                schema: "Business");

            migrationBuilder.DropIndex(
                name: "IX_GoodsIssueItems_WarehouseId",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
