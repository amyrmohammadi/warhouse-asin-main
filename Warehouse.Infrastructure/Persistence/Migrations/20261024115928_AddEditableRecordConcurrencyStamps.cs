using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditableRecordConcurrencyStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "Warehouses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "UnitsOfMeasure",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "Products",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "People",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "GoodsReceipts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "GoodsIssues",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "GoodsIssueDestinations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "UnitsOfMeasure");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "People");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "GoodsIssues");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Business",
                table: "GoodsIssueDestinations");
        }
    }
}
