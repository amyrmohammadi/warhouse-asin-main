using Warehouse.Application.Common.Models.Auditing;
using Warehouse.Application.Features.Auditing.Queries.GetAuditTrails;

namespace Warehouse.Application.Tests.Features.Auditing.Queries.GetAuditTrails;

public sealed class GetAuditTrailsFeatureTests
{
    [Fact]
    public async Task Send_WithValidQuery_ReturnsMappedPagedAuditLogs()
    {
        await using var context = new ApplicationTestContext();
        var query = new GetAuditTrailsQuery
        {
            PaginationEnabled = true,
            PageNumber = 2,
            PageSize = 25,
            Search = "Users",
            UserFullName = "مدیر",
            Action = "Update",
            TableName = "Users",
            PrimaryKey = "user-id",
            ChangedColumns = "FullName",
            SortBy = "occurredAtUtc",
            SortDescending = true
        };
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var userId = Guid.NewGuid();
        var serviceResult = new GetAuditTrailsResult(
            [
                new AuditTrailSummary(
                    42,
                    userId,
                    "مدیر سیستم",
                    "Update",
                    "Users",
                    "{\"Id\":\"user-id\"}",
                    "{\"FullName\":\"Old Name\"}",
                    "{\"FullName\":\"New Name\"}",
                    "[\"FullName\"]",
                    occurredAt)
            ],
            100,
            1,
            2,
            25,
            true);
        context.AuditTrailService
            .GetAuditTrailsAsync(
                Arg.Is<GetAuditTrailsRequest>(request =>
                    request.PageNumber == query.PageNumber &&
                    request.PageSize == query.PageSize &&
                    request.Search == query.Search &&
                    request.UserFullName == query.UserFullName &&
                    request.Action == query.Action &&
                    request.TableName == query.TableName &&
                    request.PrimaryKey == query.PrimaryKey &&
                    request.ChangedColumns == query.ChangedColumns &&
                    request.SortBy == query.SortBy &&
                    request.SortDescending == query.SortDescending),
                Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        var result = await context.Sender.Send(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(100, result.Data.TotalCount);
        Assert.Equal(1, result.Data.FilteredCount);
        Assert.True(result.Data.PaginationEnabled);
        var audit = Assert.Single(result.Data.Items);
        Assert.Equal(42, audit.Id);
        Assert.Equal(userId, audit.UserId);
        Assert.Equal("مدیر سیستم", audit.UserFullName);
        Assert.Equal("Update", audit.Action);
        Assert.Equal("Users", audit.TableName);
        Assert.Equal(occurredAt, audit.OccurredAtUtc);
    }

    [Fact]
    public async Task Send_WhenQueryIsInvalid_ThrowsValidationException()
    {
        GetAuditTrailsQuery[] invalidQueries =
        [
            new() { PageNumber = 0 },
            new() { PageSize = 0 },
            new() { PageSize = 101 },
            new() { Search = new string('s', 257) },
            new() { SortBy = "unsupported" },
            new() { Action = "Unsupported" }
        ];

        foreach (var query in invalidQueries)
        {
            await using var context = new ApplicationTestContext();

            await Assert.ThrowsAsync<ValidationException>(
                () => context.Sender.Send(query));

            await context.AuditTrailService.DidNotReceiveWithAnyArgs()
                .GetAuditTrailsAsync(default!, default);
        }
    }
}
