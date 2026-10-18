using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Warehouse.IntegrationTests.Infrastructure;

namespace Warehouse.IntegrationTests.Identity;

public sealed class UserSearchTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task GetUsers_SupportsQuickSearchAdvancedFiltersAndOptionalPagination()
    {
        using var factory = new FullStackWebApplicationFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        var accessToken = await LoginAsync(client);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"filter-{suffix}@example.com";
        var userName = $"filter-user-{suffix}";
        var fullName = $"کاربر فیلتر {suffix}";
        const string phoneNumber = "+989121234567";
        var userId = await CreateUserAsync(
            client,
            accessToken,
            email,
            userName,
            fullName,
            phoneNumber);

        var unpaged = await GetUsersAsync(client, accessToken, "/endpoint/admin/users");
        Assert.False(unpaged.PaginationEnabled);
        Assert.Equal(unpaged.TotalCount, unpaged.Items.Count);
        Assert.Contains(unpaged.Items, user => user.Id == userId);

        var paged = await GetUsersAsync(
            client,
            accessToken,
            "/endpoint/admin/users?paginationEnabled=true&pageNumber=1&pageSize=1");
        Assert.True(paged.PaginationEnabled);
        Assert.Single(paged.Items);
        Assert.Equal(1, paged.PageSize);

        foreach (var searchValue in new[] { fullName, email, userName, phoneNumber })
        {
            var searchResult = await GetUsersAsync(
                client,
                accessToken,
                "/endpoint/admin/users?paginationEnabled=true&pageNumber=1&pageSize=20" +
                $"&search={Uri.EscapeDataString(searchValue)}");
            Assert.Contains(searchResult.Items, user => user.Id == userId);
        }

        var advancedPath =
            "/endpoint/admin/users?paginationEnabled=true&pageNumber=1&pageSize=20" +
            $"&fullName={Uri.EscapeDataString(fullName)}" +
            $"&userName={Uri.EscapeDataString(userName)}" +
            $"&email={Uri.EscapeDataString(email)}" +
            "&isActive=false&roleName=User";
        var advanced = await GetUsersAsync(client, accessToken, advancedPath);
        var filteredUser = Assert.Single(advanced.Items);
        Assert.Equal(userId, filteredUser.Id);
        Assert.False(filteredUser.IsActive);
        Assert.Contains("User", filteredUser.Roles);
        Assert.Equal(1, advanced.FilteredCount);
    }

    private static async Task<Guid> CreateUserAsync(
        HttpClient client,
        string accessToken,
        string email,
        string userName,
        string fullName,
        string phoneNumber)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/endpoint/admin/users")
        {
            Content = JsonContent.Create(
                new
                {
                    email,
                    password = "Password123!",
                    fullName,
                    phoneNumber,
                    userName,
                    isActive = false,
                    roleNames = new[] { "User" }
                },
                options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 while creating filter user, received {(int)response.StatusCode}: {responseBody}");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CreateUserData>>(JsonOptions);
        return Assert.IsType<CreateUserData>(
            Assert.IsType<ApiEnvelope<CreateUserData>>(envelope).Data).UserId;
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/endpoint/auth/login",
            new
            {
                email = FullStackWebApplicationFactory.AdminEmail,
                password = FullStackWebApplicationFactory.AdminPassword
            },
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TokenData>>(JsonOptions);
        return Assert.IsType<TokenData>(
            Assert.IsType<ApiEnvelope<TokenData>>(envelope).Data).AccessToken;
    }

    private static async Task<GetUsersData> GetUsersAsync(
        HttpClient client,
        string accessToken,
        string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 for '{path}', received {(int)response.StatusCode}: {responseBody}");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<GetUsersData>>(JsonOptions);
        var result = Assert.IsType<ApiEnvelope<GetUsersData>>(envelope);
        Assert.True(result.IsSuccess);
        return Assert.IsType<GetUsersData>(result.Data);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
