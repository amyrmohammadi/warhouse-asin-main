using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityAuditProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "Warehouses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "Warehouses",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "Warehouses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "Warehouses",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "UnitsOfMeasure",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "UnitsOfMeasure",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "UnitsOfMeasure",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "UnitsOfMeasure",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "Suppliers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "Suppliers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "Suppliers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "Suppliers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "Products",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "Products",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "Products",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "Products",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "People",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "People",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "People",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "People",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "Invoices",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "Invoices",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "Invoices",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "Invoices",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "GoodsReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "GoodsReceipts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "GoodsReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "GoodsReceipts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "GoodsIssues",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "GoodsIssues",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "GoodsIssues",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "GoodsIssues",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "GoodsIssueItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "GoodsIssueItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "GoodsIssueItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "GoodsIssueItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Business",
                table: "GoodsIssueDestinations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Business",
                table: "GoodsIssueDestinations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Business",
                table: "GoodsIssueDestinations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Business",
                table: "GoodsIssueDestinations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "Audit",
                table: "AuditTrails",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOn",
                schema: "Audit",
                table: "AuditTrails",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "Audit",
                table: "AuditTrails",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOn",
                schema: "Audit",
                table: "AuditTrails",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "UnitsOfMeasure");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "UnitsOfMeasure");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "UnitsOfMeasure");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "UnitsOfMeasure");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "People");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "People");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "People");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "People");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "GoodsIssues");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "GoodsIssues");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "GoodsIssues");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "GoodsIssues");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "GoodsIssueItems");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Business",
                table: "GoodsIssueDestinations");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Business",
                table: "GoodsIssueDestinations");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Business",
                table: "GoodsIssueDestinations");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Business",
                table: "GoodsIssueDestinations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Audit",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "Audit",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "Audit",
                table: "AuditTrails");

            migrationBuilder.DropColumn(
                name: "LastModifiedOn",
                schema: "Audit",
                table: "AuditTrails");
        }
    }
}
