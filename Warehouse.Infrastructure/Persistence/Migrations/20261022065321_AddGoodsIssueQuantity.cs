using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsIssueQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                schema: "Business",
                table: "GoodsIssueItems",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                // Existing issue rows predate quantity tracking; preserve them as one item.
                defaultValue: 1m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "Business",
                table: "GoodsIssueItems");
        }
    }
}
