using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Warehouse.IntegrationTests.Identity;
using Warehouse.IntegrationTests.Infrastructure;

namespace Warehouse.IntegrationTests.MasterData;

public sealed class SupplierLookupTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Fact]
    public async Task Lookup_ReturnsMatchingSuppliersAndRejectsShortSearchTerms()
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

        await CreateSupplierAsync(client, accessToken, "تأمین‌کننده جستجو الف");
        await CreateSupplierAsync(client, accessToken, "تأمین‌کننده جستجو ب");
        await CreateSupplierAsync(client, accessToken, "تأمین‌کننده جستجو ج");
        await CreateSupplierAsync(client, accessToken, "شرکت نامرتبط");

        var matches = await SendAuthorizedAsync<List<TitledItemData>>(
            client,
            $"/endpoint/master-data/suppliers/lookup?search={Uri.EscapeDataString("جستجو")}",
            accessToken,
            HttpStatusCode.OK);
        Assert.Equal(3, matches.Count);
        Assert.All(matches, item => Assert.Contains("جستجو", item.Title));

        var limited = await SendAuthorizedAsync<List<TitledItemData>>(
            client,
            $"/endpoint/master-data/suppliers/lookup?search={Uri.EscapeDataString("جستجو")}&limit=2",
            accessToken,
            HttpStatusCode.OK);
        Assert.Equal(2, limited.Count);

        await SendAuthorizedAsync<List<TitledItemData>>(
            client,
            $"/endpoint/master-data/suppliers/lookup?search={Uri.EscapeDataString("ج")}",
            accessToken,
            HttpStatusCode.BadRequest);

        await SendAuthorizedAsync<List<TitledItemData>>(
            client,
            $"/endpoint/master-data/suppliers/lookup?search={Uri.EscapeDataString("جستجو")}&limit=31",
            accessToken,
            HttpStatusCode.BadRequest);
    }

    private static async Task CreateSupplierAsync(
        HttpClient client,
        string accessToken,
        string title)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Post,
            "/endpoint/master-data/suppliers",
            accessToken);
        request.Content = JsonContent.Create(
            new { title, description = (string?)null },
            options: JsonOptions);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected {(int)HttpStatusCode.Created}, received {(int)response.StatusCode}: {body}");
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

    private static async Task<T> SendAuthorizedAsync<T>(
        HttpClient client,
        string path,
        string accessToken,
        HttpStatusCode expectedStatus)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, path, accessToken);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus}, received {(int)response.StatusCode}: {body}");

        if (expectedStatus != HttpStatusCode.OK) return default!;

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions);
        return Assert.IsType<T>(Assert.IsType<ApiEnvelope<T>>(envelope).Data);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(
        HttpMethod method,
        string path,
        string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private sealed class TitledItemData
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string? Description { get; init; }
    }
}
