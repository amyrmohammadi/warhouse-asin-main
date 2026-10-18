using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoodsReceipts",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DelivererId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Counterparty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PageNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_People_DelivererId",
                        column: x => x.DelivererId,
                        principalSchema: "Business",
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptItems",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalSchema: "Business",
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "Business",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptWarehouses",
                schema: "Business",
                columns: table => new
                {
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptWarehouses", x => new { x.GoodsReceiptId, x.WarehouseId });
                    table.ForeignKey(
                        name: "FK_GoodsReceiptWarehouses_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalSchema: "Business",
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptWarehouses_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "Business",
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_GoodsReceiptId_ProductId",
                schema: "Business",
                table: "GoodsReceiptItems",
                columns: new[] { "GoodsReceiptId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_ProductId",
                schema: "Business",
                table: "GoodsReceiptItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_DelivererId",
                schema: "Business",
                table: "GoodsReceipts",
                column: "DelivererId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_EnteredAtUtc",
                schema: "Business",
                table: "GoodsReceipts",
                column: "EnteredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_PageNumber",
                schema: "Business",
                table: "GoodsReceipts",
                column: "PageNumber");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptWarehouses_WarehouseId",
                schema: "Business",
                table: "GoodsReceiptWarehouses",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoodsReceiptItems",
                schema: "Business");

            migrationBuilder.DropTable(
                name: "GoodsReceiptWarehouses",
                schema: "Business");

            migrationBuilder.DropTable(
                name: "GoodsReceipts",
                schema: "Business");
        }
    }
}
