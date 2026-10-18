using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkGoodsReceiptsToSuppliersAndNormalizeProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Code",
                schema: "Business",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedCode",
                schema: "Business",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedTitle",
                schema: "Business",
                table: "Products",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "Business",
                table: "GoodsReceipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [Business].[Products]
                SET
                    [NormalizedTitle] = UPPER(
                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                        [Title],
                        N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N''),
                        NCHAR(160), N''), NCHAR(8204), N''), N'-', N''), N'‐', N''),
                        N'‑', N''), N'‒', N''), N'–', N''), N'—', N''), N'―', N''),
                        N'−', N'')),
                    [NormalizedCode] = UPPER(
                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                        [Code],
                        N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N''),
                        NCHAR(160), N''), NCHAR(8204), N''), N'-', N''), N'‐', N''),
                        N'‑', N''), N'‒', N''), N'–', N''), N'—', N''), N'―', N''),
                        N'−', N''));

                IF EXISTS
                (
                    SELECT 1
                    FROM [Business].[Products]
                    GROUP BY [NormalizedTitle]
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, N'کالاهای تکراری از نظر عنوان نرمال‌شده وجود دارند؛ پیش از اجرای migration آن‌ها را اصلاح کنید.', 1;

                IF EXISTS
                (
                    SELECT 1
                    FROM [Business].[Products]
                    GROUP BY [NormalizedCode]
                    HAVING COUNT(*) > 1
                )
                    THROW 51001, N'کالاهای تکراری از نظر کد نرمال‌شده وجود دارند؛ پیش از اجرای migration آن‌ها را اصلاح کنید.', 1;

                INSERT INTO [Business].[Suppliers] ([Id], [Title], [Description])
                SELECT NEWID(), legacy.[Title], NULL
                FROM
                (
                    SELECT DISTINCT LTRIM(RTRIM([Counterparty])) AS [Title]
                    FROM [Business].[GoodsReceipts]
                    WHERE [Counterparty] IS NOT NULL
                      AND LTRIM(RTRIM([Counterparty])) <> N''
                ) AS legacy
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [Business].[Suppliers] AS supplier
                    WHERE UPPER(LTRIM(RTRIM(supplier.[Title]))) = UPPER(legacy.[Title])
                );

                UPDATE receipt
                SET [SupplierId] = matchedSupplier.[Id]
                FROM [Business].[GoodsReceipts] AS receipt
                OUTER APPLY
                (
                    SELECT TOP (1) supplier.[Id]
                    FROM [Business].[Suppliers] AS supplier
                    WHERE UPPER(LTRIM(RTRIM(supplier.[Title]))) =
                          UPPER(LTRIM(RTRIM(receipt.[Counterparty])))
                    ORDER BY supplier.[Id]
                ) AS matchedSupplier
                WHERE receipt.[Counterparty] IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "Counterparty",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.CreateIndex(
                name: "IX_Products_NormalizedCode",
                schema: "Business",
                table: "Products",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_NormalizedTitle",
                schema: "Business",
                table: "Products",
                column: "NormalizedTitle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_SupplierId",
                schema: "Business",
                table: "GoodsReceipts",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_Suppliers_SupplierId",
                schema: "Business",
                table: "GoodsReceipts",
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
                name: "FK_GoodsReceipts_Suppliers_SupplierId",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Products_NormalizedCode",
                schema: "Business",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_NormalizedTitle",
                schema: "Business",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_SupplierId",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.AddColumn<string>(
                name: "Counterparty",
                schema: "Business",
                table: "GoodsReceipts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE receipt
                SET [Counterparty] = supplier.[Title]
                FROM [Business].[GoodsReceipts] AS receipt
                INNER JOIN [Business].[Suppliers] AS supplier
                    ON supplier.[Id] = receipt.[SupplierId];
                """);

            migrationBuilder.DropColumn(
                name: "NormalizedCode",
                schema: "Business",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NormalizedTitle",
                schema: "Business",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "Business",
                table: "GoodsReceipts");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                schema: "Business",
                table: "Products",
                column: "Code",
                unique: true);
        }
    }
}
