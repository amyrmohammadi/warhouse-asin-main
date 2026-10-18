using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyGoodsReceiptWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                schema: "Business",
                table: "GoodsReceipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestNumber",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE receipt
                SET WarehouseId = selectedWarehouse.WarehouseId
                FROM [Business].[GoodsReceipts] AS receipt
                CROSS APPLY
                (
                    SELECT TOP (1) link.WarehouseId
                    FROM [Business].[GoodsReceiptWarehouses] AS link
                    WHERE link.GoodsReceiptId = receipt.Id
                    ORDER BY link.WarehouseId
                ) AS selectedWarehouse;

                UPDATE item
                SET RequestNumber = receipt.RequestNumber
                FROM [Business].[GoodsReceiptItems] AS item
                INNER JOIN [Business].[GoodsReceipts] AS receipt
                    ON receipt.Id = item.GoodsReceiptId;
                """);

            migrationBuilder.DropTable(
                name: "GoodsReceiptWarehouses",
                schema: "Business");

            migrationBuilder.DropColumn(
                name: "RequestNumber",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                schema: "Business",
                table: "GoodsReceipts",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_WarehouseId",
                schema: "Business",
                table: "GoodsReceipts",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_Warehouses_WarehouseId",
                schema: "Business",
                table: "GoodsReceipts",
                column: "WarehouseId",
                principalSchema: "Business",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_Warehouses_WarehouseId",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_WarehouseId",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.AddColumn<string>(
                name: "RequestNumber",
                schema: "Business",
                table: "GoodsReceipts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE receipt
                SET RequestNumber = selectedItem.RequestNumber
                FROM [Business].[GoodsReceipts] AS receipt
                OUTER APPLY
                (
                    SELECT TOP (1) item.RequestNumber
                    FROM [Business].[GoodsReceiptItems] AS item
                    WHERE item.GoodsReceiptId = receipt.Id
                      AND item.RequestNumber IS NOT NULL
                    ORDER BY item.Id
                ) AS selectedItem;
                """);

            migrationBuilder.CreateTable(
                name: "GoodsReceiptWarehouses",
                schema: "Business",
                columns: table => new
                {
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptWarehouses", x => new { x.GoodsReceiptId, x.WarehouseId });
                    table.ForeignKey(
                        name: "FK_GoodsReceiptWarehouses_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalSchema: "Business",
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptWarehouses_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "Business",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptWarehouses_WarehouseId",
                schema: "Business",
                table: "GoodsReceiptWarehouses",
                column: "WarehouseId");

            migrationBuilder.Sql(
                """
                INSERT INTO [Business].[GoodsReceiptWarehouses] (GoodsReceiptId, WarehouseId)
                SELECT Id, WarehouseId
                FROM [Business].[GoodsReceipts];
                """);

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "RequestNumber",
                schema: "Business",
                table: "GoodsReceiptItems");
        }
    }
}
