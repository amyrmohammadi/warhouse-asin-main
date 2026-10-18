using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Warehouse.IntegrationTests.Infrastructure;

namespace Warehouse.IntegrationTests.Identity;

public sealed class RoleSearchTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task GetRoles_SupportsOptionalPaginationAndSqlNumericFilters()
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

        var unpaged = await GetRolesAsync(client, accessToken, "/endpoint/admin/roles");
        Assert.False(unpaged.PaginationEnabled);
        Assert.Equal(unpaged.TotalCount, unpaged.Items.Count);
        Assert.True(unpaged.TotalCount >= 2);

        var paged = await GetRolesAsync(
            client,
            accessToken,
            "/endpoint/admin/roles?paginationEnabled=true&pageNumber=1&pageSize=1&sortBy=name");
        Assert.True(paged.PaginationEnabled);
        Assert.Single(paged.Items);
        Assert.Equal(1, paged.PageSize);
        Assert.Equal(unpaged.TotalCount, paged.TotalCount);

        var operators = new[] { "eq", "neq", "gt", "gte", "lt", "lte", "between" };
        foreach (var numericOperator in operators)
        {
            var value = numericOperator is "lt" or "lte" ? 100 : 0;
            var valueTo = numericOperator == "between" ? "&userCount.valueTo=100" : string.Empty;
            var path =
                "/endpoint/admin/roles?paginationEnabled=true&pageNumber=1&pageSize=10" +
                $"&userCount.operator={numericOperator}&userCount.value={value}{valueTo}";

            _ = await GetRolesAsync(client, accessToken, path);
        }

        var combined = await GetRolesAsync(
            client,
            accessToken,
            "/endpoint/admin/roles?paginationEnabled=true&pageNumber=1&pageSize=10" +
            "&search=Admin&permissionCount.operator=gte&permissionCount.value=0");
        Assert.All(
            combined.Items,
            role => Assert.Contains("Admin", role.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(combined.Items.Count, combined.FilteredCount);
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
        var result = Assert.IsType<ApiEnvelope<TokenData>>(envelope);
        Assert.True(result.IsSuccess);
        return Assert.IsType<TokenData>(result.Data).AccessToken;
    }

    private static async Task<GetRolesData> GetRolesAsync(
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
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<GetRolesData>>(JsonOptions);
        var result = Assert.IsType<ApiEnvelope<GetRolesData>>(envelope);
        Assert.True(result.IsSuccess);
        return Assert.IsType<GetRolesData>(result.Data);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
