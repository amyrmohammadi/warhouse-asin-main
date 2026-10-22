using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceiptItemSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "Business",
                table: "GoodsReceiptItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                WITH OrderedItems AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY GoodsReceiptId ORDER BY Id) - 1 AS SortOrder
                    FROM [Business].[GoodsReceiptItems]
                )
                UPDATE item
                SET item.SortOrder = ordered.SortOrder
                FROM [Business].[GoodsReceiptItems] AS item
                INNER JOIN OrderedItems AS ordered ON ordered.Id = item.Id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "Business",
                table: "GoodsReceiptItems");
        }
    }
}
