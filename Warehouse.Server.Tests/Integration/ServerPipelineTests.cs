using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MediatR;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Identity.Commands.AddPoliciesToRole;
using Warehouse.Application.Features.Identity.Commands.AddRolesToUser;
using Warehouse.Application.Features.Identity.Commands.Login;
using Warehouse.Application.Features.Identity.Commands.SetRolesForUser;
using Warehouse.Application.Features.Identity.Queries.GetRoles;
using Warehouse.Application.Features.Identity.Queries.GetUsers;
using Warehouse.Application.Features.Auditing.Queries.GetAuditTrails;

namespace Warehouse.Server.Tests.Integration;

public sealed class ServerPipelineTests
{
    [Fact]
    public async Task AuthEndpoint_WhenUnhandledExceptionOccurs_ReturnsTraceableProblemDetails()
    {
        await using var factory = new TestWebApplicationFactory();
        factory.Sender
            .Send(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<Result<LoginResponse>>(
                new InvalidOperationException("Expected test exception.")));
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/endpoint/auth/login",
            new { email = "user@example.com", password = "Password1!" });
        var body = await response.Content.ReadAsStringAsync();
        using var problemDetails = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(
            "An unexpected server error occurred.",
            problemDetails.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            problemDetails.RootElement.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData("/endpoint/auth/register")]
    [InlineData("/endpoint/auth/login")]
    [InlineData("/endpoint/auth/refresh-token")]
    public async Task AuthEndpoints_AllowAnonymousRequestsToReachModelBinding(string path)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(path, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/endpoint/admin/users", null)]
    [InlineData("GET", "/endpoint/admin/roles", null)]
    [InlineData("GET", "/endpoint/admin/audit-logs", null)]
    [InlineData("PUT", "/endpoint/admin/users/89df72e3-fb63-479f-b22a-79cf25b05a82", "{\"fullName\":\"Test User\",\"phoneNumber\":\"+989121234567\",\"email\":\"user@example.com\",\"userName\":\"user\"}")]
    [InlineData("PUT", "/endpoint/admin/users/89df72e3-fb63-479f-b22a-79cf25b05a82/active-status", "{\"isActive\":true}")]
    [InlineData("PUT", "/endpoint/admin/users/89df72e3-fb63-479f-b22a-79cf25b05a82/password", "{\"newPassword\":\"NewPassword1!\"}")]
    [InlineData("PUT", "/endpoint/admin/users/89df72e3-fb63-479f-b22a-79cf25b05a82/roles", null)]
    [InlineData("POST", "/endpoint/admin/users/89df72e3-fb63-479f-b22a-79cf25b05a82/roles", "{\"roleNames\":[\"User\"]}")]
    [InlineData("POST", "/endpoint/admin/roles/Admin/policies", "{\"policies\":[\"UsersRead\"]}")]
    public async Task ManagementEndpoints_WhenAnonymous_ReturnUnauthorized(
        string method,
        string path,
        string? body)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest(method, path, body);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/endpoint/admin/users", null)]
    [InlineData("GET", "/endpoint/admin/roles", null)]
    [InlineData("GET", "/endpoint/admin/audit-logs", null)]
    [InlineData("PUT", "/endpoint/admin/users/89df72e3-fb63-479f-b22a-79cf25b05a82", "{\"fullName\":\"Test User\",\"phoneNumber\":\"+989121234567\",\"email\":\"user@example.com\",\"userName\":\"user\"}")]
    [InlineData("PUT", "/endpoint/admin/users/89df72e3-fb63-479f-b22a-79cf25b05a82/active-status", "{\"isActive\":true}")]
    [InlineData("PUT", "/endpoint/admin/users/89df72e3-fb63-479f-b22a-79cf25b05a82/password", "{\"newPassword\":\"NewPassword1!\"}")]
    [InlineData("PUT", "/endpoint/admin/users/89df72e3-fb63-479f-b22a-79cf25b05a82/roles", null)]
    [InlineData("POST", "/endpoint/admin/users/89df72e3-fb63-479f-b22a-79cf25b05a82/roles", "{\"roleNames\":[\"User\"]}")]
    [InlineData("POST", "/endpoint/admin/roles/Admin/policies", "{\"policies\":[\"UsersRead\"]}")]
    public async Task ManagementEndpoints_WhenPermissionIsMissing_ReturnForbidden(
        string method,
        string path,
        string? body)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest(method, path, body);
        request.Headers.Add(TestAuthenticationDefaults.UserHeader, "true");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithUsersReadPermission_ReachesMediator()
    {
        await using var factory = new TestWebApplicationFactory();
        var expected = Result<GetUsersResponse>.Success(
            new GetUsersResponse([], 0, 0, 2, 10, true));
        factory.Sender
            .Send(
                Arg.Is<GetUsersQuery>(query =>
                    query.PageNumber == 2 &&
                    query.PageSize == 10 &&
                    query.PaginationEnabled &&
                    query.Search == "ali" &&
                    query.SortBy == "createdAtUtc" &&
                    query.SortDescending),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "GET",
            "/endpoint/admin/users?paginationEnabled=true&pageNumber=2&pageSize=10&search=ali&sortBy=createdAtUtc&sortDescending=true");
        AddIdentity(request, PermissionPolicyNames.UserManagement.UsersRead);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_WithRolesReadPermission_ReachesMediator()
    {
        await using var factory = new TestWebApplicationFactory();
        var expected = Result<GetRolesResponse>.Success(
            new GetRolesResponse([], 0, 0, 1, 20, false));
        factory.Sender
            .Send(Arg.Any<GetRolesQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        using var client = factory.CreateClient();
        using var request = CreateRequest("GET", "/endpoint/admin/roles");
        AddIdentity(request, PermissionPolicyNames.RoleManagement.RolesRead);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await factory.Sender.Received(1).Send(
            Arg.Any<GetRolesQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuditLogs_WithAuditLogsReadPermission_ReachesMediator()
    {
        await using var factory = new TestWebApplicationFactory();
        var expected = Result<GetAuditTrailsResponse>.Success(
            new GetAuditTrailsResponse([], 0, 0, 2, 10, true));
        factory.Sender
            .Send(
                Arg.Is<GetAuditTrailsQuery>(query =>
                    query.PageNumber == 2 &&
                    query.PageSize == 10 &&
                    query.PaginationEnabled &&
                    query.Search == "Users" &&
                    query.Action == "Update" &&
                    query.SortBy == "occurredAtUtc" &&
                    query.SortDescending),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "GET",
            "/endpoint/admin/audit-logs?paginationEnabled=true&pageNumber=2&pageSize=10&search=Users&action=Update&sortBy=occurredAtUtc&sortDescending=true");
        AddIdentity(request, PermissionPolicyNames.Auditing.AuditLogsRead);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AssignRoles_WithUserRolesManagePermission_MapsJsonAndRouteValues()
    {
        await using var factory = new TestWebApplicationFactory();
        var userId = Guid.NewGuid();
        factory.Sender
            .Send(Arg.Any<AddRolesToUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "POST",
            $"/endpoint/admin/users/{userId}/roles",
            "{\"roleNames\":[\"Admin\",\"User\"]}");
        AddIdentity(request, PermissionPolicyNames.UserManagement.UserRolesManage);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await factory.Sender.Received(1).Send(
            Arg.Is<AddRolesToUserCommand>(command =>
                command.UserId == userId &&
                command.RoleNames.SequenceEqual(new[] { "Admin", "User" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetRoles_WithUserRolesManagePermission_MapsJsonAndRouteValues()
    {
        await using var factory = new TestWebApplicationFactory();
        var userId = Guid.NewGuid();
        factory.Sender
            .Send(Arg.Any<SetRolesForUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/endpoint/admin/users/{userId}/roles")
        {
            Content = JsonContent.Create(new { roleNames = new[] { "Admin" } })
        };
        AddIdentity(request, PermissionPolicyNames.UserManagement.UserRolesManage);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await factory.Sender.Received(1).Send(
            Arg.Is<SetRolesForUserCommand>(command =>
                command.UserId == userId &&
                command.RoleNames.SequenceEqual(new[] { "Admin" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignPolicies_WithRolePoliciesManagePermission_AcceptsStringEnums()
    {
        await using var factory = new TestWebApplicationFactory();
        factory.Sender
            .Send(Arg.Any<AddPoliciesToRoleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "POST",
            "/endpoint/admin/roles/Admin/policies",
            "{\"policies\":[\"UsersRead\",\"RolePoliciesManage\"]}");
        AddIdentity(request, PermissionPolicyNames.RoleManagement.RolePoliciesManage);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await factory.Sender.Received(1).Send(
            Arg.Is<AddPoliciesToRoleCommand>(command =>
                command.RoleName == "Admin" &&
                command.Policies.SequenceEqual(
                    new[] { "UsersRead", "RolePoliciesManage" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Swagger_InDevelopment_DescribesTheServerApi()
    {
        await using var factory = new TestWebApplicationFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/endpoint/auth/login", document, StringComparison.Ordinal);
        Assert.Contains("/endpoint/admin/users", document, StringComparison.Ordinal);
        Assert.Contains("/endpoint/admin/roles", document, StringComparison.Ordinal);
        Assert.Contains("/endpoint/admin/audit-logs", document, StringComparison.Ordinal);
        Assert.Contains("UsersRead", document, StringComparison.Ordinal);
    }

    private static HttpRequestMessage CreateRequest(
        string method,
        string path,
        string? body = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static void AddIdentity(HttpRequestMessage request, string permission)
    {
        request.Headers.Add(TestAuthenticationDefaults.UserHeader, "true");
        request.Headers.Add(TestAuthenticationDefaults.PermissionsHeader, permission);
    }
}
