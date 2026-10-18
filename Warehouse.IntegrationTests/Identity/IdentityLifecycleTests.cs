using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application.Common.Authorization;
using Warehouse.Infrastructure.Identity;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Infrastructure.Persistence.Auditing;
using Warehouse.IntegrationTests.Infrastructure;

namespace Warehouse.IntegrationTests.Identity;

public sealed class IdentityLifecycleTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task IdentityLifecycle_UsesRealHttpPipelineIdentityJwtAndRelationalDatabase()
    {
        using var factory = new FullStackWebApplicationFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var adminTokens = await LoginAsync(
            client,
            FullStackWebApplicationFactory.AdminEmail,
            FullStackWebApplicationFactory.AdminPassword);
        Assert.Equal(
            Enum.GetValues<AppPermission>().Order(),
            adminTokens.Permissions.Order());

        using (var usersResponse = await SendAuthorizedAsync(
                   client,
                   HttpMethod.Get,
                   "/endpoint/admin/users?pageNumber=1&pageSize=20",
                   adminTokens.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
            var users = await ReadAsync<GetUsersData>(usersResponse);
            Assert.True(users.Data!.TotalCount >= 2);
            Assert.Contains(
                users.Data.Items,
                user =>
                    user.Email == FullStackWebApplicationFactory.AdminEmail &&
                    user.Roles.Contains(DefaultRoles.Admin));
            Assert.Contains(
                users.Data.Items,
                user =>
                    user.Email == FullStackWebApplicationFactory.UserEmail &&
                    user.Roles.Contains(DefaultRoles.User));
        }

        const string newUserEmail = "created.integration@example.com";
        const string newUserPassword = "Created123!";
        const string changedUserPassword = "Changed123!";
        using var registerResponse = await client.PostAsJsonAsync(
            "/endpoint/auth/register",
            new
            {
                email = newUserEmail,
                password = newUserPassword,
                fullName = "Created Integration User",
                phoneNumber = "+989121234567",
                userName = "created-user"
            },
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var createdUser = await ReadAsync<CreateUserData>(registerResponse);
        Assert.True(createdUser.IsSuccess);
        Assert.NotEqual(Guid.Empty, createdUser.Data!.UserId);

        using (var duplicateRegistration = await client.PostAsJsonAsync(
                   "/endpoint/auth/register",
                   new
                   {
                       email = newUserEmail,
                       password = newUserPassword,
                       fullName = "Duplicate Integration User",
                       phoneNumber = "+989121234568",
                       userName = "another-name"
                   },
                   JsonOptions))
        {
            Assert.Equal(HttpStatusCode.BadRequest, duplicateRegistration.StatusCode);
        }

        using (var pendingLoginResponse = await client.PostAsJsonAsync(
                   "/endpoint/auth/login",
                   new { email = newUserEmail, password = newUserPassword },
                   JsonOptions))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, pendingLoginResponse.StatusCode);
        }

        using (var activateResponse = await SendAuthorizedJsonAsync(
                   client,
                   $"/endpoint/admin/users/{createdUser.Data.UserId}/active-status",
                   adminTokens.AccessToken,
                   new { isActive = true },
                   HttpMethod.Put))
        {
            Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        }

        using (var updateUserResponse = await SendAuthorizedJsonAsync(
                   client,
                   $"/endpoint/admin/users/{createdUser.Data.UserId}",
                   adminTokens.AccessToken,
                   new
                   {
                       fullName = "Updated Integration User",
                       phoneNumber = "+989123456789",
                       email = newUserEmail,
                       userName = "updated-created-user"
                   },
                   HttpMethod.Put))
        {
            Assert.Equal(HttpStatusCode.OK, updateUserResponse.StatusCode);
        }

        using (var changePasswordResponse = await SendAuthorizedJsonAsync(
                   client,
                   $"/endpoint/admin/users/{createdUser.Data.UserId}/password",
                   adminTokens.AccessToken,
                   new { newPassword = changedUserPassword },
                   HttpMethod.Put))
        {
            Assert.Equal(HttpStatusCode.OK, changePasswordResponse.StatusCode);
        }

        using (var oldPasswordResponse = await client.PostAsJsonAsync(
                   "/endpoint/auth/login",
                   new { email = newUserEmail, password = newUserPassword },
                   JsonOptions))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordResponse.StatusCode);
        }

        using (var assignRoleResponse = await SendAuthorizedJsonAsync(
                   client,
                   $"/endpoint/admin/users/{createdUser.Data.UserId}/roles",
                   adminTokens.AccessToken,
                   new { roleNames = new[] { DefaultRoles.User } }))
        {
            Assert.Equal(HttpStatusCode.OK, assignRoleResponse.StatusCode);
        }

        var tokensBeforePolicyChange = await LoginAsync(
            client,
            newUserEmail,
            changedUserPassword);
        Assert.Empty(tokensBeforePolicyChange.Permissions);

        using (var assignPolicyResponse = await SendAuthorizedJsonAsync(
                   client,
                   $"/endpoint/admin/roles/{DefaultRoles.User}/policies",
                   adminTokens.AccessToken,
                   new { policies = new[] { AppPermission.UsersRead } },
                   HttpMethod.Put))
        {
            Assert.Equal(HttpStatusCode.OK, assignPolicyResponse.StatusCode);
        }

        using (var staleAccessTokenResponse = await SendAuthorizedAsync(
                   client,
                   HttpMethod.Get,
                   "/endpoint/admin/users",
                   tokensBeforePolicyChange.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, staleAccessTokenResponse.StatusCode);
        }

        using var refreshedAfterPolicyChangeResponse = await client.PostAsJsonAsync(
                   "/endpoint/auth/refresh-token",
                   new { refreshToken = tokensBeforePolicyChange.RefreshToken },
                   JsonOptions);
        Assert.Equal(HttpStatusCode.OK, refreshedAfterPolicyChangeResponse.StatusCode);
        var refreshedAfterPolicyChange = await ReadAsync<TokenData>(
            refreshedAfterPolicyChangeResponse);
        Assert.True(refreshedAfterPolicyChange.IsSuccess);
        var tokensAfterPolicyChange = Assert.IsType<TokenData>(
            refreshedAfterPolicyChange.Data);
        Assert.Equal([AppPermission.UsersRead], tokensAfterPolicyChange.Permissions);

        using (var permittedUsersResponse = await SendAuthorizedAsync(
                   client,
                   HttpMethod.Get,
                   "/endpoint/admin/users?search=created.integration",
                   tokensAfterPolicyChange.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, permittedUsersResponse.StatusCode);
            var users = await ReadAsync<GetUsersData>(permittedUsersResponse);
            var persistedUser = Assert.Single(users.Data!.Items);
            Assert.Equal(createdUser.Data.UserId, persistedUser.Id);
            Assert.Equal("Updated Integration User", persistedUser.FullName);
            Assert.Equal("+989123456789", persistedUser.PhoneNumber);
            Assert.True(persistedUser.IsActive);
            Assert.Equal([DefaultRoles.User], persistedUser.Roles);
        }

        using (var removePolicyResponse = await SendAuthorizedJsonAsync(
                   client,
                   $"/endpoint/admin/roles/{DefaultRoles.User}/policies",
                   adminTokens.AccessToken,
                   new { policies = Array.Empty<AppPermission>() },
                   HttpMethod.Put))
        {
            Assert.Equal(HttpStatusCode.OK, removePolicyResponse.StatusCode);
        }

        using (var revokedPermissionResponse = await SendAuthorizedAsync(
                   client,
                   HttpMethod.Get,
                   "/endpoint/admin/users",
                   tokensAfterPolicyChange.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, revokedPermissionResponse.StatusCode);
        }

        using (var restorePolicyResponse = await SendAuthorizedJsonAsync(
                   client,
                   $"/endpoint/admin/roles/{DefaultRoles.User}/policies",
                   adminTokens.AccessToken,
                   new { policies = new[] { AppPermission.UsersRead } },
                   HttpMethod.Put))
        {
            Assert.Equal(HttpStatusCode.OK, restorePolicyResponse.StatusCode);
        }

        using (var forbiddenRoleManagement = await SendAuthorizedJsonAsync(
                   client,
                   $"/endpoint/admin/users/{createdUser.Data.UserId}/roles",
                   tokensAfterPolicyChange.AccessToken,
                   new { roleNames = new[] { DefaultRoles.Admin } }))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenRoleManagement.StatusCode);
        }

        string? updatedAuditActorName = null;
        using (var auditLogsResponse = await SendAuthorizedAsync(
                   client,
                   HttpMethod.Get,
                   "/endpoint/admin/audit-logs?paginationEnabled=true&pageNumber=1&pageSize=100&search=Users",
                   adminTokens.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, auditLogsResponse.StatusCode);
            var auditLogs = await ReadAsync<GetAuditLogsData>(auditLogsResponse);
            Assert.True(auditLogs.Data!.PaginationEnabled);
            Assert.All(
                auditLogs.Data.Items,
                audit => Assert.False(string.IsNullOrWhiteSpace(audit.UserFullName)));
            Assert.Contains(
                auditLogs.Data.Items,
                audit =>
                    audit.UserId == createdUser.Data.UserId &&
                    audit.TableName == "Users" &&
                    audit.Action == "Create");
            Assert.Contains(
                auditLogs.Data.Items,
                audit =>
                    audit.UserId == adminTokens.UserId &&
                    audit.TableName == "Users" &&
                    audit.Action == "Update" &&
                    audit.NewValues?.Contains(
                        "Updated Integration User",
                        StringComparison.Ordinal) == true);
            updatedAuditActorName = auditLogs.Data.Items
                .First(audit =>
                    audit.TableName == "Users" &&
                    audit.Action == "Update" &&
                    audit.NewValues?.Contains(
                        "Updated Integration User",
                        StringComparison.Ordinal) == true)
                .UserFullName;
        }

        Assert.False(string.IsNullOrWhiteSpace(updatedAuditActorName));
        var advancedAuditPath =
            "/endpoint/admin/audit-logs?paginationEnabled=true&pageNumber=1&pageSize=100" +
            "&action=Update&tableName=Users&changedColumns=FullName" +
            $"&primaryKey={createdUser.Data.UserId}" +
            $"&userFullName={Uri.EscapeDataString(updatedAuditActorName)}";
        using (var advancedAuditResponse = await SendAuthorizedAsync(
                   client,
                   HttpMethod.Get,
                   advancedAuditPath,
                   adminTokens.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, advancedAuditResponse.StatusCode);
            var auditLogs = await ReadAsync<GetAuditLogsData>(advancedAuditResponse);
            Assert.Contains(
                auditLogs.Data!.Items,
                audit =>
                    audit.TableName == "Users" &&
                    audit.Action == "Update" &&
                    audit.ChangedColumns?.Contains("FullName", StringComparison.Ordinal) == true);
        }

        using var refreshResponse = await client.PostAsJsonAsync(
            "/endpoint/auth/refresh-token",
            new { refreshToken = tokensAfterPolicyChange.RefreshToken },
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotatedTokens = await ReadAsync<TokenData>(refreshResponse);
        Assert.True(rotatedTokens.IsSuccess);
        Assert.NotEqual(
            tokensAfterPolicyChange.RefreshToken,
            rotatedTokens.Data!.RefreshToken);

        using (var reusedTokenResponse = await client.PostAsJsonAsync(
                   "/endpoint/auth/refresh-token",
                   new { refreshToken = tokensAfterPolicyChange.RefreshToken },
                   JsonOptions))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, reusedTokenResponse.StatusCode);
        }

        using (var compromisedReplacementResponse = await client.PostAsJsonAsync(
                   "/endpoint/auth/refresh-token",
                   new { refreshToken = rotatedTokens.Data.RefreshToken },
                   JsonOptions))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, compromisedReplacementResponse.StatusCode);
        }

        await AssertPersistedRelationshipsAsync(
            factory.Services,
            createdUser.Data.UserId,
            newUserEmail,
            changedUserPassword);
    }

    private static async Task<TokenData> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/endpoint/auth/login",
            new { email, password },
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await ReadAsync<TokenData>(response);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RefreshToken));

        return result.Data;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string accessToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAuthorizedJsonAsync<T>(
        HttpClient client,
        string path,
        string accessToken,
        T body,
        HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static async Task<ApiEnvelope<T>> ReadAsync<T>(HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions);
        return Assert.IsType<ApiEnvelope<T>>(result);
    }

    private static async Task AssertPersistedRelationshipsAsync(
        IServiceProvider services,
        Guid userId,
        string email,
        string changedUserPassword)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await database.Users.SingleAsync(user => user.Id == userId);
        Assert.Equal(email.ToUpperInvariant(), user.NormalizedEmail);
        Assert.Equal("Updated Integration User", user.FullName);
        Assert.Equal("+989123456789", user.PhoneNumber);
        Assert.True(user.IsActive);

        var role = await database.Roles.SingleAsync(
            role => role.NormalizedName == DefaultRoles.User.ToUpperInvariant());
        Assert.True(await database.UserRoles.AnyAsync(
            userRole => userRole.UserId == userId && userRole.RoleId == role.Id));
        Assert.True(await database.RoleClaims.AnyAsync(
            claim =>
                claim.RoleId == role.Id &&
                claim.ClaimType == ApplicationClaimTypes.Permission &&
                claim.ClaimValue == PermissionPolicyNames.UserManagement.UsersRead));

        var adminUserId = await database.Users
            .Where(user => user.Email == FullStackWebApplicationFactory.AdminEmail)
            .Select(user => user.Id)
            .SingleAsync();
        var userAuditTrails = await database.AuditTrails
            .AsNoTracking()
            .Where(audit =>
                audit.UserId == adminUserId &&
                audit.TableName == "Users" &&
                audit.Action == AuditAction.Update)
            .ToListAsync();

        Assert.Contains(
            userAuditTrails,
            audit => audit.NewValues?.Contains(
                "Updated Integration User",
                StringComparison.Ordinal) == true);

        var passwordAudit = Assert.Single(userAuditTrails, audit =>
            audit.ChangedColumns?.Contains(
                "PasswordHash",
                StringComparison.Ordinal) == true);
        Assert.Contains("[REDACTED]", passwordAudit.NewValues);
        Assert.DoesNotContain(changedUserPassword, passwordAudit.NewValues);

        await database.Database.OpenConnectionAsync();
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM [Identity].[RefreshTokens]";
        var refreshTokenCount = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.True(refreshTokenCount >= 4);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
