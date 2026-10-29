using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicesAndGoodsIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptItems_Suppliers_SupplierId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId", schema: "Business", table: "GoodsReceiptItems",
                type: "uniqueidentifier", nullable: true);

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
                name: "GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoodsIssueDestinations",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsIssueDestinations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoodsIssues",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IssuerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsIssues_People_IssuerId",
                        column: x => x.IssuerId,
                        principalSchema: "Business",
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "UnderReview"),
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

            migrationBuilder.CreateTable(
                name: "GoodsIssueItems",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsIssueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AssetNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DestinationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShippingLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExitReasonType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Other"),
                    IsReturnable = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ExitReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeliveringUnit = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReturnAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsIssueItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsIssueItems_GoodsIssueDestinations_DestinationId",
                        column: x => x.DestinationId,
                        principalSchema: "Business",
                        principalTable: "GoodsIssueDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsIssueItems_GoodsIssues_GoodsIssueId",
                        column: x => x.GoodsIssueId,
                        principalSchema: "Business",
                        principalTable: "GoodsIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsIssueItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "Business",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsIssueItems_Suppliers_ShippingLocationId",
                        column: x => x.ShippingLocationId,
                        principalSchema: "Business",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // The local receipt day is part of the invoice identity, independently of InvoiceDate.
            migrationBuilder.Sql(
                """
                ;WITH InvoiceKeys AS
                (
                    SELECT LTRIM(RTRIM(item.[InvoiceNumber])) AS [InvoiceNumber],
                           item.[SupplierId],
                           CONVERT(date, receipt.[EnteredAtUtc] AT TIME ZONE 'Iran Standard Time') AS [ReceiptDate]
                    FROM [Business].[GoodsReceiptItems] AS item
                    INNER JOIN [Business].[GoodsReceipts] AS receipt ON receipt.[Id] = item.[GoodsReceiptId]
                    WHERE NULLIF(LTRIM(RTRIM(item.[InvoiceNumber])), N'') IS NOT NULL
                    GROUP BY LTRIM(RTRIM(item.[InvoiceNumber])), item.[SupplierId],
                             CONVERT(date, receipt.[EnteredAtUtc] AT TIME ZONE 'Iran Standard Time')
                )
                INSERT INTO [Business].[Invoices] ([Id], [InvoiceNumber], [SupplierId], [ReceiptDate])
                SELECT NEWID(), [InvoiceNumber], [SupplierId], [ReceiptDate]
                FROM InvoiceKeys;

                UPDATE item
                SET item.[InvoiceId] = invoice.[Id]
                FROM [Business].[GoodsReceiptItems] AS item
                INNER JOIN [Business].[GoodsReceipts] AS receipt ON receipt.[Id] = item.[GoodsReceiptId]
                INNER JOIN [Business].[Invoices] AS invoice
                    ON invoice.[InvoiceNumber] = LTRIM(RTRIM(item.[InvoiceNumber]))
                    AND (invoice.[SupplierId] = item.[SupplierId]
                         OR (invoice.[SupplierId] IS NULL AND item.[SupplierId] IS NULL))
                    AND invoice.[ReceiptDate] =
                        CONVERT(date, receipt.[EnteredAtUtc] AT TIME ZONE 'Iran Standard Time');

                IF EXISTS
                (
                    SELECT 1 FROM [Business].[GoodsReceiptItems] AS item
                    INNER JOIN [Business].[GoodsReceipts] AS receipt ON receipt.[Id] = item.[GoodsReceiptId]
                    LEFT JOIN [Business].[Invoices] AS invoice ON invoice.[Id] = item.[InvoiceId]
                    WHERE NULLIF(LTRIM(RTRIM(item.[InvoiceNumber])), N'') IS NOT NULL
                    AND (invoice.[Id] IS NULL
                        OR invoice.[InvoiceNumber] <> LTRIM(RTRIM(item.[InvoiceNumber]))
                        OR invoice.[SupplierId] <> item.[SupplierId]
                        OR (invoice.[SupplierId] IS NULL AND item.[SupplierId] IS NOT NULL)
                        OR (invoice.[SupplierId] IS NOT NULL AND item.[SupplierId] IS NULL)
                        OR invoice.[ReceiptDate] IS NULL
                        OR invoice.[ReceiptDate] <>
                            CONVERT(date, receipt.[EnteredAtUtc] AT TIME ZONE 'Iran Standard Time'))
                )
                    THROW 51003, N'Invoice migration failed: invoice number, supplier or receipt day was not preserved.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_SupplierId", schema: "Business", table: "GoodsReceiptItems");
            migrationBuilder.DropColumn(
                name: "InvoiceNumber", schema: "Business", table: "GoodsReceiptItems");
            migrationBuilder.DropColumn(
                name: "SupplierId", schema: "Business", table: "GoodsReceiptItems");
            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_InvoiceId", schema: "Business",
                table: "GoodsReceiptItems", column: "InvoiceId");

            migrationBuilder.Sql(
                """
                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue])
                SELECT legacy.[RoleId], legacy.[ClaimType], permissions.[ClaimValue]
                FROM [Identity].[RoleClaims] AS legacy
                CROSS APPLY (VALUES
                    (N'GoodsIssuesCreate'),
                    (N'GoodsIssuesUpdate'),
                    (N'GoodsIssuesDelete')) AS permissions([ClaimValue])
                WHERE legacy.[ClaimType] = N'permission'
                  AND legacy.[ClaimValue] = N'GoodsIssuesManage'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [Identity].[RoleClaims] AS existing
                      WHERE existing.[RoleId] = legacy.[RoleId]
                        AND existing.[ClaimType] = legacy.[ClaimType]
                        AND existing.[ClaimValue] = permissions.[ClaimValue]);

                DELETE FROM [Identity].[RoleClaims]
                WHERE [ClaimType] = N'permission'
                  AND [ClaimValue] = N'GoodsIssuesManage';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "GoodsIssueItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueDestinations_Title",
                schema: "Business",
                table: "GoodsIssueDestinations",
                column: "Title",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueItems_DestinationId",
                schema: "Business",
                table: "GoodsIssueItems",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueItems_GoodsIssueId",
                schema: "Business",
                table: "GoodsIssueItems",
                column: "GoodsIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueItems_ProductId",
                schema: "Business",
                table: "GoodsIssueItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssueItems_ShippingLocationId",
                schema: "Business",
                table: "GoodsIssueItems",
                column: "ShippingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssues_IssuedAtUtc",
                schema: "Business",
                table: "GoodsIssues",
                column: "IssuedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssues_IssueNumber",
                schema: "Business",
                table: "GoodsIssues",
                column: "IssueNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsIssues_IssuerId",
                schema: "Business",
                table: "GoodsIssues",
                column: "IssuerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber_SupplierId_ReceiptDate",
                schema: "Business",
                table: "Invoices",
                columns: new[] { "InvoiceNumber", "SupplierId", "ReceiptDate" },
                unique: true,
                filter: "[SupplierId] IS NOT NULL AND [ReceiptDate] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SupplierId",
                schema: "Business",
                table: "Invoices",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptItems_GoodsIssueItems_GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "GoodsIssueItemId",
                principalSchema: "Business",
                principalTable: "GoodsIssueItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
            migrationBuilder.Sql(
                """
                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue])
                SELECT DISTINCT permission.[RoleId], permission.[ClaimType], N'GoodsIssuesManage'
                FROM [Identity].[RoleClaims] AS permission
                WHERE permission.[ClaimType] = N'permission'
                  AND permission.[ClaimValue] IN (
                      N'GoodsIssuesCreate', N'GoodsIssuesUpdate', N'GoodsIssuesDelete')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [Identity].[RoleClaims] AS existing
                      WHERE existing.[RoleId] = permission.[RoleId]
                        AND existing.[ClaimType] = permission.[ClaimType]
                        AND existing.[ClaimValue] = N'GoodsIssuesManage');

                DELETE FROM [Identity].[RoleClaims]
                WHERE [ClaimType] = N'permission'
                  AND [ClaimValue] IN (
                      N'GoodsIssuesCreate', N'GoodsIssuesUpdate', N'GoodsIssuesDelete');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptItems_GoodsIssueItems_GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptItems_Invoices_InvoiceId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropTable(
                name: "GoodsIssueItems",
                schema: "Business");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber", schema: "Business", table: "GoodsReceiptItems",
                type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId", schema: "Business", table: "GoodsReceiptItems",
                type: "uniqueidentifier", nullable: true);
            // Compile the restoration query after adding the legacy columns.
            migrationBuilder.Sql(
                """
                EXEC(N'UPDATE receiptItem
                SET
                    receiptItem.[InvoiceNumber] = invoice.[InvoiceNumber],
                    receiptItem.[SupplierId] = invoice.[SupplierId]
                FROM [Business].[GoodsReceiptItems] AS receiptItem
                INNER JOIN [Business].[Invoices] AS invoice
                    ON invoice.[Id] = receiptItem.[InvoiceId];');
                """);
            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_InvoiceId", schema: "Business", table: "GoodsReceiptItems");
            migrationBuilder.DropColumn(
                name: "InvoiceId", schema: "Business", table: "GoodsReceiptItems");
            migrationBuilder.DropTable(name: "Invoices", schema: "Business");
            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_SupplierId", schema: "Business",
                table: "GoodsReceiptItems", column: "SupplierId");

            migrationBuilder.DropTable(
                name: "GoodsIssueDestinations",
                schema: "Business");

            migrationBuilder.DropTable(
                name: "GoodsIssues",
                schema: "Business");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems");

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
                name: "GoodsIssueItemId",
                schema: "Business",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "Price",
                schema: "Business",
                table: "GoodsReceiptItems");

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
