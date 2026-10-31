using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceCreatedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "Business",
                table: "Invoices",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            // Restore historical creation times where the audit trail has them.
            // Legacy invoices created by the invoice migration have no creation audit;
            // use their earliest linked receipt, then their receipt day as a fallback.
            migrationBuilder.Sql(
                """
                ;WITH CreationTimes AS
                (
                    SELECT TRY_CONVERT(uniqueidentifier,
                               JSON_VALUE(CASE WHEN ISJSON([PrimaryKey]) = 1
                                   THEN [PrimaryKey] ELSE N'{}' END, '$.Id')) AS [InvoiceId],
                           MIN([OccurredAtUtc]) AS [CreatedAtUtc]
                    FROM [Audit].[AuditTrails]
                    WHERE [TableName] = N'Invoices' AND [Action] = N'Create'
                    GROUP BY TRY_CONVERT(uniqueidentifier,
                               JSON_VALUE(CASE WHEN ISJSON([PrimaryKey]) = 1
                                   THEN [PrimaryKey] ELSE N'{}' END, '$.Id'))
                ), ReceiptTimes AS
                (
                    SELECT item.[InvoiceId], MIN(receipt.[EnteredAtUtc]) AS [EnteredAtUtc]
                    FROM [Business].[GoodsReceiptItems] AS item
                    INNER JOIN [Business].[GoodsReceipts] AS receipt ON receipt.[Id] = item.[GoodsReceiptId]
                    WHERE item.[InvoiceId] IS NOT NULL
                    GROUP BY item.[InvoiceId]
                )
                UPDATE invoice
                SET [CreatedAtUtc] = COALESCE(creation.[CreatedAtUtc], receipt.[EnteredAtUtc],
                    CAST(invoice.[ReceiptDate] AS datetime2) AT TIME ZONE 'Iran Standard Time',
                    invoice.[CreatedAtUtc])
                FROM [Business].[Invoices] AS invoice
                LEFT JOIN CreationTimes AS creation ON creation.[InvoiceId] = invoice.[Id]
                LEFT JOIN ReceiptTimes AS receipt ON receipt.[InvoiceId] = invoice.[Id];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreatedAtUtc_Id",
                schema: "Business",
                table: "Invoices",
                columns: new[] { "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_CreatedAtUtc_Id",
                schema: "Business",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "Business",
                table: "Invoices");
        }
    }
}
