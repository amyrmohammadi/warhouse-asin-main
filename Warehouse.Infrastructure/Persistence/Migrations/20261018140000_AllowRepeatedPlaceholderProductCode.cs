using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations;

public partial class AllowRepeatedPlaceholderProductCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Products_NormalizedCode", schema: "Business", table: "Products");
        migrationBuilder.CreateIndex(
            name: "IX_Products_NormalizedCode", schema: "Business", table: "Products",
            column: "NormalizedCode", unique: true, filter: "[Code] <> N'-'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Products_NormalizedCode", schema: "Business", table: "Products");
        migrationBuilder.CreateIndex(
            name: "IX_Products_NormalizedCode", schema: "Business", table: "Products",
            column: "NormalizedCode", unique: true);
    }
}
