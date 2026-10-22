using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Warehouse.IntegrationTests.Identity;
using Warehouse.IntegrationTests.Infrastructure;

namespace Warehouse.IntegrationTests.MasterData;

public sealed class ProductLookupTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Fact]
    public async Task Lookup_ReturnsMatchingProductsAndRejectsShortSearchTerms()
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

        await CreateUnitAsync(client, accessToken, "عدد");
        await CreateProductAsync(client, accessToken, "کالای جستجو الف", "JA1", "عدد");
        await CreateProductAsync(client, accessToken, "کالای جستجو ب", "JB2", "عدد");
        await CreateProductAsync(client, accessToken, "ماشین‌آلات", "X9", "عدد");

        var matches = await SendAuthorizedAsync<List<ProductData>>(
            client,
            $"/endpoint/master-data/products/lookup?search={Uri.EscapeDataString("جستجو")}",
            accessToken,
            HttpStatusCode.OK);
        Assert.Equal(2, matches.Count);
        Assert.All(matches, item => Assert.Contains("جستجو", item.Title));
        Assert.All(matches, item => Assert.Equal("عدد", item.UnitOfMeasureTitle));

        var byCode = await SendAuthorizedAsync<List<ProductData>>(
            client,
            $"/endpoint/master-data/products/lookup?search={Uri.EscapeDataString("JB2")}",
            accessToken,
            HttpStatusCode.OK);
        Assert.Single(byCode);
        Assert.Equal("کالای جستجو ب", byCode.Single().Title);

        var limited = await SendAuthorizedAsync<List<ProductData>>(
            client,
            $"/endpoint/master-data/products/lookup?search={Uri.EscapeDataString("جستجو")}&limit=1",
            accessToken,
            HttpStatusCode.OK);
        Assert.Single(limited);

        await SendAuthorizedAsync<List<ProductData>>(
            client,
            $"/endpoint/master-data/products/lookup?search={Uri.EscapeDataString("ج")}",
            accessToken,
            HttpStatusCode.BadRequest);

        await SendAuthorizedAsync<List<ProductData>>(
            client,
            $"/endpoint/master-data/products/lookup?search={Uri.EscapeDataString("جستجو")}&limit=31",
            accessToken,
            HttpStatusCode.BadRequest);
    }

    private static async Task CreateUnitAsync(
        HttpClient client,
        string accessToken,
        string title)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Post,
            "/endpoint/master-data/units-of-measure",
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

    private static async Task CreateProductAsync(
        HttpClient client,
        string accessToken,
        string title,
        string code,
        string unitOfMeasureTitle)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Post,
            "/endpoint/master-data/products",
            accessToken);
        request.Content = JsonContent.Create(
            new { title, code, unitOfMeasureTitle, description = (string?)null },
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

    private sealed class ProductData
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;

        public Guid UnitOfMeasureId { get; init; }

        public string UnitOfMeasureTitle { get; init; } = string.Empty;

        public string? Description { get; init; }
    }
}
