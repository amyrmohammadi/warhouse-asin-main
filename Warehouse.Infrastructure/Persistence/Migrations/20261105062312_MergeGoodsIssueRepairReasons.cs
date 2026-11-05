using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeGoodsIssueRepairReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [Business].[GoodsIssueItems]
                SET [ExitReasonType] = N'Repair',
                    [ExitReason] = CASE
                        WHEN LTRIM(RTRIM([ExitReason])) IN (N'تعمیرات', N'Maintenance') THEN N'تعمیر'
                        ELSE [ExitReason]
                    END
                WHERE [ExitReasonType] = N'Maintenance'
                   OR LTRIM(RTRIM([ExitReason])) IN (N'تعمیرات', N'Maintenance');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Merged historical reasons cannot be distinguished from existing Repair rows.
        }
    }
}
