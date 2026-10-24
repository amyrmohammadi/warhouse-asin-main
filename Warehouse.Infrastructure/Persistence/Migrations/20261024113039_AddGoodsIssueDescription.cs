using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsIssueDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "Business",
                table: "GoodsIssues",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

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

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "Business",
                table: "GoodsIssues");
        }
    }
}
