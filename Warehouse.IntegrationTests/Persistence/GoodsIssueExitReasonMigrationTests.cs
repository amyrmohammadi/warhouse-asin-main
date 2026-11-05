using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure.Persistence;
using Xunit;

namespace Warehouse.IntegrationTests.Persistence;

public sealed class GoodsIssueExitReasonMigrationTests
{
    [Fact]
    public async Task Migration_ConvertsOldRepairRows_AndPreservesOtherReasons()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database=Warehouse_ExitReasonTests_{Guid.NewGuid():N};" +
                "Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        await using var context = new ApplicationDbContext(options);
        try
        {
            await context.GetService<IMigrator>().MigrateAsync("20261103053249_AddEntityAuditProperties");
            var person = new Person("خارج‌کننده", "");
            var issue = new GoodsIssue("OUT-1", DateTimeOffset.UtcNow, null, person.Id);
            var old = issue.AddItem(null, "OLD", 1, null, null, "تعمیر", null, null, null, 0);
            var repair = issue.AddItem(null, "REPAIR", 1, null, null, "تعمیر", null, null, null, 1);
            var sale = issue.AddItem(null, "SALE", 1, null, null, "فروش", null, null, null, 2);
            context.Add(person);
            await context.SaveChangesAsync();
            // Seed the old schema directly: today's entity also has columns introduced by later migrations.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [Business].[GoodsIssues] ([Id], [IssueNumber], [IssuedAtUtc], [IssuerId], [IsFinalized], [ConcurrencyStamp])
                VALUES ({issue.Id}, {issue.IssueNumber}, {issue.IssuedAtUtc}, {person.Id}, 0, {issue.ConcurrencyStamp})
                """);
            foreach (var item in issue.Items)
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [Business].[GoodsIssueItems] ([Id], [GoodsIssueId], [ProductCode], [Quantity], [SortOrder], [ExitReasonType], [ExitReason], [IsReturnable])
                    VALUES ({item.Id}, {issue.Id}, {item.ProductCode}, {item.Quantity}, {item.SortOrder}, {item.ExitReasonType.ToString()}, {item.ExitReason}, 1)
                    """);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [Business].[GoodsIssueItems]
                SET [ExitReasonType] = N'Maintenance', [ExitReason] = N'تعمیرات'
                WHERE [Id] = {old.Id}
                """);
            context.ChangeTracker.Clear();

            await context.Database.MigrateAsync();

            var rows = await context.Set<GoodsIssueItem>().ToDictionaryAsync(item => item.Id);
            Assert.Equal(GoodsIssueExitReason.Repair, rows[old.Id].ExitReasonType);
            Assert.Equal("تعمیر", rows[old.Id].ExitReason);
            Assert.Equal(GoodsIssueExitReason.Repair, rows[repair.Id].ExitReasonType);
            Assert.Equal(GoodsIssueExitReason.Sale, rows[sale.Id].ExitReasonType);
            Assert.Equal("فروش", rows[sale.Id].ExitReason);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}
