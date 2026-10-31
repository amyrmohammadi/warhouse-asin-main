using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceiptItemSupplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptItems_Suppliers_SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "SupplierId",
                principalSchema: "Business",
                principalTable: "Suppliers",
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

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems");
        }
    }
}
