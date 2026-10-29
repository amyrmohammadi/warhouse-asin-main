using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusAndReasonToIssueTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNumber_SupplierId",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.AddColumn<string>(
                name: "ExitReasonType",
                schema: "Business",
                table: "GoodsIssueItems",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnable",
                schema: "Business",
                table: "GoodsIssueItems",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShippingLocationId",
                schema: "Business",
                table: "GoodsIssueItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber_SupplierId",
                schema: "Business",
                table: "Invoices",
                columns: new[] { "InvoiceNumber", "SupplierId" },
                unique: true,
                filter: "[SupplierId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueItems_ShippingLocationId",
                schema: "Business",
                table: "GoodsIssueItems",
                column: "ShippingLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsIssueItems_Suppliers_ShippingLocationId",
                schema: "Business",
                table: "GoodsIssueItems",
                column: "ShippingLocationId",
                principalSchema: "Business",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsIssueItems_Suppliers_ShippingLocationId",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNumber_SupplierId",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_GoodsIssueItems_ShippingLocationId",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.DropColumn(
                name: "ExitReasonType",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.DropColumn(
                name: "IsReturnable",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.DropColumn(
                name: "ShippingLocationId",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber_SupplierId",
                schema: "Business",
                table: "Invoices",
                columns: new[] { "InvoiceNumber", "SupplierId" },
                unique: true);
        }
    }
}
