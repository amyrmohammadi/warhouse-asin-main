using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeAndDeduplicatePeople : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedFullName",
                schema: "Business",
                table: "People",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE [Business].[People]
                SET [NormalizedFullName] = UPPER(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    [FullName],
                    N' ', N''), NCHAR(9), N''), NCHAR(10), N''), NCHAR(13), N''),
                    NCHAR(160), N''), NCHAR(8204), N''), N'-', N''), N'‐', N''),
                    N'‑', N''), N'‒', N''), N'–', N''), N'—', N''), N'―', N''),
                    N'−', N''));

                IF EXISTS
                (
                    SELECT 1
                    FROM [Business].[People]
                    GROUP BY [NormalizedFullName]
                    HAVING COUNT(*) > 1
                )
                    THROW 51002, N'تحویل‌دهندگان تکراری از نظر نام نرمال‌شده وجود دارند؛ پیش از اجرای migration آن‌ها را اصلاح کنید.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_People_NormalizedFullName",
                schema: "Business",
                table: "People",
                column: "NormalizedFullName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_People_NormalizedFullName",
                schema: "Business",
                table: "People");

            migrationBuilder.DropColumn(
                name: "NormalizedFullName",
                schema: "Business",
                table: "People");
        }
    }
}
