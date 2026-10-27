using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkReceiptItemsToIssueItemsAndExtendInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "Business",
                table: "Invoices",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InvoiceDate",
                schema: "Business",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingStatus",
                schema: "Business",
                table: "Invoices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "UnderReview");

            migrationBuilder.AddColumn<Guid>(
                name: "GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "GoodsIssueItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptItems_GoodsIssueItems_GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "GoodsIssueItemId",
                principalSchema: "Business",
                principalTable: "GoodsIssueItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptItems_GoodsIssueItems_GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceDate",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems");
        }
    }
}
