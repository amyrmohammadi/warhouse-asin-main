using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveGoodsReceiptDetailsToItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_Suppliers_SupplierId",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_Warehouses_WarehouseId",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_SupplierId",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_WarehouseId",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceStatus",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """
                UPDATE item
                SET item.WarehouseId = receipt.WarehouseId,
                    item.InvoiceStatus = receipt.InvoiceStatus,
                    item.InvoiceNumber = receipt.InvoiceNumber,
                    item.SupplierId = receipt.SupplierId,
                    item.ReceiptNumber = receipt.ReceiptNumber
                FROM [Business].[GoodsReceiptItems] AS item
                INNER JOIN [Business].[GoodsReceipts] AS receipt
                    ON receipt.Id = item.GoodsReceiptId;
                """);

            migrationBuilder.DropColumn(name: "InvoiceNumber", schema: "Business", table: "GoodsReceipts");
            migrationBuilder.DropColumn(name: "InvoiceStatus", schema: "Business", table: "GoodsReceipts");
            migrationBuilder.DropColumn(name: "ReceiptNumber", schema: "Business", table: "GoodsReceipts");
            migrationBuilder.DropColumn(name: "SupplierId", schema: "Business", table: "GoodsReceipts");
            migrationBuilder.DropColumn(name: "WarehouseId", schema: "Business", table: "GoodsReceipts");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_WarehouseId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptItems_Suppliers_SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "SupplierId",
                principalSchema: "Business",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptItems_Warehouses_WarehouseId",
                schema: "Business",
                table: "GoodsReceiptItems",
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
                name: "FK_GoodsReceiptItems_Suppliers_SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptItems_Warehouses_WarehouseId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_WarehouseId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                schema: "Business",
                table: "GoodsReceipts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceStatus",
                schema: "Business",
                table: "GoodsReceipts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                schema: "Business",
                table: "GoodsReceipts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "Business",
                table: "GoodsReceipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                schema: "Business",
                table: "GoodsReceipts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """
                UPDATE receipt
                SET receipt.WarehouseId = source.WarehouseId,
                    receipt.InvoiceStatus = source.InvoiceStatus,
                    receipt.InvoiceNumber = source.InvoiceNumber,
                    receipt.SupplierId = source.SupplierId,
                    receipt.ReceiptNumber = source.ReceiptNumber
                FROM [Business].[GoodsReceipts] AS receipt
                CROSS APPLY (
                    SELECT TOP (1)
                        item.WarehouseId,
                        item.InvoiceStatus,
                        item.InvoiceNumber,
                        item.SupplierId,
                        item.ReceiptNumber
                    FROM [Business].[GoodsReceiptItems] AS item
                    WHERE item.GoodsReceiptId = receipt.Id
                    ORDER BY item.Id
                ) AS source;
                """);

            migrationBuilder.DropColumn(name: "InvoiceNumber", schema: "Business", table: "GoodsReceiptItems");
            migrationBuilder.DropColumn(name: "InvoiceStatus", schema: "Business", table: "GoodsReceiptItems");
            migrationBuilder.DropColumn(name: "ReceiptNumber", schema: "Business", table: "GoodsReceiptItems");
            migrationBuilder.DropColumn(name: "SupplierId", schema: "Business", table: "GoodsReceiptItems");
            migrationBuilder.DropColumn(name: "WarehouseId", schema: "Business", table: "GoodsReceiptItems");
            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_SupplierId",
                schema: "Business",
                table: "GoodsReceipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_WarehouseId",
                schema: "Business",
                table: "GoodsReceipts",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_Suppliers_SupplierId",
                schema: "Business",
                table: "GoodsReceipts",
                column: "SupplierId",
                principalSchema: "Business",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
    }
}
