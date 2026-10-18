using Warehouse.Application.Features.Identity.Queries.GetUsers;

namespace Warehouse.Application.Tests.Features.Identity.Queries.GetUsers;

public sealed class GetUsersFeatureTests
{
    [Fact]
    public async Task Send_WithValidQuery_ReturnsMappedPagedUsers()
    {
        await using var context = new ApplicationTestContext();
        var query = new GetUsersQuery
        {
            PaginationEnabled = true,
            PageNumber = 2,
            PageSize = 25,
            Search = "amir",
            FullName = "Amir",
            UserName = "amir",
            Email = "amir@example.com",
            IsActive = false,
            RoleName = "Admin",
            SortBy = "email",
            SortDescending = true
        };
        var userId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
        byte[] profileImage = [0x89, 0x50, 0x4E, 0x47];
        var serviceResult = new GetUsersResult(
            [
                new UserSummary(
                    userId,
                    "amir",
                    "amir@example.com",
                    "Amir Ahmadi",
                    "+989121234567",
                    false,
                    true,
                    createdAt,
                    ["Admin"],
                    "image/png",
                    profileImage)
            ],
            31,
            1,
            2,
            25,
            true);
        context.IdentityService
            .GetUsersAsync(
                Arg.Is<GetUsersRequest>(request =>
                    request.PageNumber == query.PageNumber &&
                    request.PageSize == query.PageSize &&
                    request.Search == query.Search &&
                    request.FullName == query.FullName &&
                    request.UserName == query.UserName &&
                    request.Email == query.Email &&
                    request.IsActive == query.IsActive &&
                    request.RoleName == query.RoleName &&
                    request.SortBy == query.SortBy &&
                    request.SortDescending == query.SortDescending),
                Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        var result = await context.Sender.Send(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(31, result.Data.TotalCount);
        Assert.Equal(1, result.Data.FilteredCount);
        Assert.Equal(2, result.Data.PageNumber);
        Assert.Equal(25, result.Data.PageSize);
        Assert.True(result.Data.PaginationEnabled);
        var user = Assert.Single(result.Data.Items);
        Assert.Equal(userId, user.Id);
        Assert.Equal("amir", user.UserName);
        Assert.Equal("amir@example.com", user.Email);
        Assert.Equal("Amir Ahmadi", user.FullName);
        Assert.Equal("+989121234567", user.PhoneNumber);
        Assert.False(user.IsActive);
        Assert.True(user.EmailConfirmed);
        Assert.Equal(createdAt, user.CreatedAtUtc);
        Assert.Equal(["Admin"], user.Roles);
        Assert.Equal("image/png", user.ProfileImageContentType);
        Assert.Equal(profileImage, user.ProfileImage);
    }

    [Fact]
    public async Task Send_WhenQueryIsInvalid_ThrowsValidationException()
    {
        GetUsersQuery[] invalidQueries =
        [
            new() { PageNumber = 0 },
            new() { PageSize = 0 },
            new() { PageSize = 101 },
            new() { Search = new string('s', 257) },
            new() { SortBy = "unsupported" }
        ];

        foreach (var query in invalidQueries)
        {
            await using var context = new ApplicationTestContext();

            await Assert.ThrowsAsync<ValidationException>(
                () => context.Sender.Send(query));

            await context.IdentityService.DidNotReceiveWithAnyArgs()
                .GetUsersAsync(default!, default);
        }
    }
}
