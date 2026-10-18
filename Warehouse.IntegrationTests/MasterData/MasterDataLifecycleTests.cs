using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Warehouse.IntegrationTests.Identity;
using Warehouse.IntegrationTests.Infrastructure;

namespace Warehouse.IntegrationTests.MasterData;

public sealed class MasterDataLifecycleTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task MasterDataEndpoints_SupportCrudSearchAndPagination()
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

        await AssertTitledValidationContractAsync(
            client,
            accessToken,
            "/endpoint/master-data/units-of-measure");
        await AssertTitledValidationContractAsync(
            client,
            accessToken,
            "/endpoint/master-data/suppliers");
        await AssertTitledValidationContractAsync(
            client,
            accessToken,
            "/endpoint/master-data/warehouses");
        await AssertPersonValidationContractAsync(client, accessToken);
        await AssertTitledResourceLifecycleAsync(
            client,
            accessToken,
            "/endpoint/master-data/units-of-measure",
            "عدد");
        await AssertTitledResourceLifecycleAsync(
            client,
            accessToken,
            "/endpoint/master-data/suppliers",
            "تامین‌کننده تست");
        await AssertTitledResourceLifecycleAsync(
            client,
            accessToken,
            "/endpoint/master-data/warehouses",
            "انبار تست");
        await AssertPersonLifecycleAsync(client, accessToken);
        await AssertSupplierImportAsync(client, accessToken);
    }

    private static async Task AssertTitledResourceLifecycleAsync(
        HttpClient client,
        string accessToken,
        string path,
        string title)
    {
        var created = await SendAuthorizedJsonAsync<TitledItemData>(
            client,
            HttpMethod.Post,
            path,
            accessToken,
            new { title, description = "توضیحات اولیه" },
            HttpStatusCode.Created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(title, created.Title);

        var fetched = await SendAuthorizedAsync<TitledItemData>(
            client,
            HttpMethod.Get,
            $"{path}/{created.Id}",
            accessToken,
            HttpStatusCode.OK);
        Assert.Equal(created.Id, fetched.Id);

        var updatedTitle = $"{title} ویرایش‌شده";
        var updated = await SendAuthorizedJsonAsync<TitledItemData>(
            client,
            HttpMethod.Put,
            $"{path}/{created.Id}",
            accessToken,
            new { title = updatedTitle, description = "توضیحات جدید" },
            HttpStatusCode.OK);
        Assert.Equal(updatedTitle, updated.Title);

        var list = await SendAuthorizedAsync<MasterDataListData<TitledItemData>>(
            client,
            HttpMethod.Get,
            $"{path}?paginationEnabled=true&pageNumber=1&pageSize=1&search={Uri.EscapeDataString(updatedTitle)}",
            accessToken,
            HttpStatusCode.OK);
        Assert.True(list.PaginationEnabled);
        Assert.Single(list.Items);
        Assert.Equal(created.Id, list.Items[0].Id);
        Assert.Equal(1, list.FilteredCount);

        var filteredList = await SendAuthorizedAsync<MasterDataListData<TitledItemData>>(
            client,
            HttpMethod.Get,
            $"{path}?title={Uri.EscapeDataString(updatedTitle)}&description={Uri.EscapeDataString("توضیحات جدید")}",
            accessToken,
            HttpStatusCode.OK);
        Assert.Single(filteredList.Items);
        Assert.Equal(created.Id, filteredList.Items[0].Id);

        await SendAuthorizedWithoutDataAsync(
            client,
            HttpMethod.Delete,
            $"{path}/{created.Id}",
            accessToken,
            HttpStatusCode.OK);
        await SendAuthorizedWithoutDataAsync(
            client,
            HttpMethod.Get,
            $"{path}/{created.Id}",
            accessToken,
            HttpStatusCode.NotFound);
    }

    private static async Task AssertTitledValidationContractAsync(
        HttpClient client,
        string accessToken,
        string path)
    {
        var rules = await SendAuthorizedAsync<Dictionary<string, FieldValidationRulesData>>(
            client,
            HttpMethod.Get,
            $"{path}/validation-rules",
            accessToken,
            HttpStatusCode.OK);

        Assert.True(rules["title"].Required);
        Assert.Equal(200, rules["title"].MaxLength);
        Assert.False(rules["description"].Required);
        Assert.Equal(1000, rules["description"].MaxLength);
    }

    private static async Task AssertPersonValidationContractAsync(
        HttpClient client,
        string accessToken)
    {
        var rules = await SendAuthorizedAsync<Dictionary<string, FieldValidationRulesData>>(
            client,
            HttpMethod.Get,
            "/endpoint/master-data/people/validation-rules",
            accessToken,
            HttpStatusCode.OK);

        Assert.True(rules["fullName"].Required);
        Assert.Equal(200, rules["fullName"].MaxLength);
        Assert.True(rules["phoneNumber"].Required);
        Assert.Equal(32, rules["phoneNumber"].MaxLength);
    }

    private static async Task AssertPersonLifecycleAsync(
        HttpClient client,
        string accessToken)
    {
        const string path = "/endpoint/master-data/people";
        var created = await SendAuthorizedJsonAsync<PersonData>(
            client,
            HttpMethod.Post,
            path,
            accessToken,
            new { fullName = "شخص تست", phoneNumber = "09120000000" },
            HttpStatusCode.Created);
        Assert.NotEqual(Guid.Empty, created.Id);

        var updated = await SendAuthorizedJsonAsync<PersonData>(
            client,
            HttpMethod.Put,
            $"{path}/{created.Id}",
            accessToken,
            new { fullName = "شخص ویرایش‌شده", phoneNumber = "09121111111" },
            HttpStatusCode.OK);
        Assert.Equal("09121111111", updated.PhoneNumber);

        var list = await SendAuthorizedAsync<MasterDataListData<PersonData>>(
            client,
            HttpMethod.Get,
            $"{path}?search=09121111111",
            accessToken,
            HttpStatusCode.OK);
        Assert.False(list.PaginationEnabled);
        Assert.Single(list.Items);
        Assert.Equal(created.Id, list.Items[0].Id);

        var filteredList = await SendAuthorizedAsync<MasterDataListData<PersonData>>(
            client,
            HttpMethod.Get,
            $"{path}?fullName={Uri.EscapeDataString("شخص ویرایش‌شده")}&phoneNumber=09121111111",
            accessToken,
            HttpStatusCode.OK);
        Assert.Single(filteredList.Items);
        Assert.Equal(created.Id, filteredList.Items[0].Id);

        await SendAuthorizedWithoutDataAsync(
            client,
            HttpMethod.Delete,
            $"{path}/{created.Id}",
            accessToken,
            HttpStatusCode.OK);
        await SendAuthorizedWithoutDataAsync(
            client,
            HttpMethod.Get,
            $"{path}/{created.Id}",
            accessToken,
            HttpStatusCode.NotFound);
    }

    private static async Task AssertSupplierImportAsync(
        HttpClient client,
        string accessToken)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Suppliers");
        worksheet.Cell("B1").Value = "Supplier Name";
        worksheet.Cell("B2").Value = "Imported Supplier";
        worksheet.Cell("B3").Value = "Imported Supplier";
        using var workbookStream = new MemoryStream();
        workbook.SaveAs(workbookStream);

        using var request = CreateAuthorizedRequest(
            HttpMethod.Post,
            "/endpoint/master-data/suppliers/import",
            accessToken);
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(workbookStream.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "File", "suppliers.xlsx");
        request.Content = content;

        using var response = await client.SendAsync(request);
        await AssertStatusAsync(response, HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<SupplierImportSummaryData>>(
            JsonOptions);
        var summary = Assert.IsType<SupplierImportSummaryData>(
            Assert.IsType<ApiEnvelope<SupplierImportSummaryData>>(envelope).Data);
        Assert.Equal(2, summary.ProcessedRowCount);
        Assert.Equal(1, summary.ImportedCount);
        Assert.Equal(1, summary.DuplicateCount);
        Assert.Empty(summary.InvalidRows);

        var list = await SendAuthorizedAsync<MasterDataListData<TitledItemData>>(
            client,
            HttpMethod.Get,
            "/endpoint/master-data/suppliers?search=Imported%20Supplier",
            accessToken,
            HttpStatusCode.OK);
        Assert.Single(list.Items);
        Assert.Equal("Imported Supplier", list.Items[0].Title);
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
        HttpMethod method,
        string path,
        string accessToken,
        HttpStatusCode expectedStatus)
    {
        using var request = CreateAuthorizedRequest(method, path, accessToken);
        using var response = await client.SendAsync(request);
        await AssertStatusAsync(response, expectedStatus);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions);
        return Assert.IsType<T>(Assert.IsType<ApiEnvelope<T>>(envelope).Data);
    }

    private static async Task<T> SendAuthorizedJsonAsync<T>(
        HttpClient client,
        HttpMethod method,
        string path,
        string accessToken,
        object body,
        HttpStatusCode expectedStatus)
    {
        using var request = CreateAuthorizedRequest(method, path, accessToken);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await client.SendAsync(request);
        await AssertStatusAsync(response, expectedStatus);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions);
        return Assert.IsType<T>(Assert.IsType<ApiEnvelope<T>>(envelope).Data);
    }

    private static async Task SendAuthorizedWithoutDataAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string accessToken,
        HttpStatusCode expectedStatus)
    {
        using var request = CreateAuthorizedRequest(method, path, accessToken);
        using var response = await client.SendAsync(request);
        await AssertStatusAsync(response, expectedStatus);
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

    private static async Task AssertStatusAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus}, received {(int)response.StatusCode}: {body}");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class MasterDataListData<T>
    {
        public List<T> Items { get; init; } = [];

        public int TotalCount { get; init; }

        public int FilteredCount { get; init; }

        public int PageNumber { get; init; }

        public int PageSize { get; init; }

        public bool PaginationEnabled { get; init; }
    }

    private sealed class TitledItemData
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string? Description { get; init; }
    }

    private sealed class FieldValidationRulesData
    {
        public bool Required { get; init; }

        public int? MaxLength { get; init; }
    }

    private sealed class PersonData
    {
        public Guid Id { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string PhoneNumber { get; init; } = string.Empty;
    }

    private sealed class SupplierImportSummaryData
    {
        public int ProcessedRowCount { get; init; }

        public int ImportedCount { get; init; }

        public int DuplicateCount { get; init; }

        public List<SupplierImportRowErrorData> InvalidRows { get; init; } = [];
    }

    private sealed class SupplierImportRowErrorData
    {
        public int RowNumber { get; init; }

        public string Message { get; init; } = string.Empty;
    }
}
