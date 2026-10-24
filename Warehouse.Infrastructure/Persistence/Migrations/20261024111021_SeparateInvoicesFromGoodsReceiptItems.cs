using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateInvoicesFromGoodsReceiptItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptItems_Suppliers_SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "Business",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                ;WITH ExistingInvoiceNumbers AS
                (
                    SELECT
                        LTRIM(RTRIM([InvoiceNumber])) AS [InvoiceNumber],
                        [SupplierId],
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY LTRIM(RTRIM([InvoiceNumber]))
                            ORDER BY CASE WHEN [SupplierId] IS NULL THEN 1 ELSE 0 END, [Id]
                        ) AS [RowNumber]
                    FROM [Business].[GoodsReceiptItems]
                    WHERE NULLIF(LTRIM(RTRIM([InvoiceNumber])), N'') IS NOT NULL
                )
                INSERT INTO [Business].[Invoices] ([Id], [InvoiceNumber], [SupplierId])
                SELECT NEWID(), [InvoiceNumber], [SupplierId]
                FROM ExistingInvoiceNumbers
                WHERE [RowNumber] = 1;

                UPDATE receiptItem
                SET receiptItem.[InvoiceId] = invoice.[Id]
                FROM [Business].[GoodsReceiptItems] AS receiptItem
                INNER JOIN [Business].[Invoices] AS invoice
                    ON invoice.[InvoiceNumber] = LTRIM(RTRIM(receiptItem.[InvoiceNumber]));
                """);

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                schema: "Business",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SupplierId",
                schema: "Business",
                table: "Invoices",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_InvoiceId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptItems_Invoices_InvoiceId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "InvoiceId",
                principalSchema: "Business",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptItems_Invoices_InvoiceId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
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

            migrationBuilder.Sql(
                """
                UPDATE receiptItem
                SET
                    receiptItem.[InvoiceNumber] = invoice.[InvoiceNumber],
                    receiptItem.[SupplierId] = invoice.[SupplierId]
                FROM [Business].[GoodsReceiptItems] AS receiptItem
                INNER JOIN [Business].[Invoices] AS invoice
                    ON invoice.[Id] = receiptItem.[InvoiceId];
                """);

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_InvoiceId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "Business");

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
    }
}
