using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireAuditActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [Audit].[AuditTrails] " +
                "SET [UserName] = N'System' " +
                "WHERE [UserName] IS NULL OR LTRIM(RTRIM([UserName])) = N'';");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                schema: "Audit",
                table: "AuditTrails",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "System",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                schema: "Audit",
                table: "AuditTrails",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldDefaultValue: "System");
        }
    }
}
