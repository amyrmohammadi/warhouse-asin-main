using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsIssueFinalizationAndInvoiceFinanceDeliveryDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FinanceDeliveryDate",
                schema: "Business",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinalized",
                schema: "Business",
                table: "GoodsIssues",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinanceDeliveryDate",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IsFinalized",
                schema: "Business",
                table: "GoodsIssues");
        }
    }
}
