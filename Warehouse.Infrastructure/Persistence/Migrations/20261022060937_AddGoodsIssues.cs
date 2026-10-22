using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoodsIssueDestinations",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
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
                    IssuerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                name: "GoodsIssueItems",
                schema: "Business",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsIssueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssetNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DestinationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExitReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeliveringUnit = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
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
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoodsIssueItems",
                schema: "Business");

            migrationBuilder.DropTable(
                name: "GoodsIssueDestinations",
                schema: "Business");

            migrationBuilder.DropTable(
                name: "GoodsIssues",
                schema: "Business");
        }
    }
}
