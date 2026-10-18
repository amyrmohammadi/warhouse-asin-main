using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Infrastructure.Persistence;
using Warehouse.IntegrationTests.Identity;
using Warehouse.IntegrationTests.Infrastructure;

namespace Warehouse.IntegrationTests.Inventory;

public sealed class GoodsReceiptCreationTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Create_WithTypedDelivererSupplierAndProduct_LinksReferencesAndRejectsNormalizedDuplicates()
    {
        using var factory = new FullStackWebApplicationFactory();
        await factory.InitializeDatabaseAsync();
        var (warehouseId, unitOfMeasureId) = await SeedReferencesAsync(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var accessToken = await LoginAsync(client);

        var created = await CreateReceiptAsync(
            client,
            accessToken,
            null,
            "تحویل‌دهنده- جدید",
            warehouseId,
            unitOfMeasureId,
            "تأمین‌کننده جدید",
            "کالای-آزمایشی",
            "ab- 12",
            HttpStatusCode.Created);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.NotEqual(Guid.Empty, created.DelivererId);
        Assert.Equal("تحویل‌دهنده- جدید", created.DelivererFullName);
        Assert.NotNull(created.SupplierId);
        Assert.Equal("تأمین‌کننده جدید", created.Counterparty);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var receipt = await database.GoodsReceipts
                .Include(item => item.Deliverer)
                .Include(item => item.Supplier)
                .Include(item => item.Items)
                    .ThenInclude(item => item.Product)
                .SingleAsync(item => item.Id == created.Id);

            Assert.Equal(created.SupplierId, receipt.SupplierId);
            Assert.Equal(created.DelivererId, receipt.DelivererId);
            Assert.Equal(
                TextComparison.CreateCompactKey("تحویل دهنده جدید"),
                receipt.Deliverer.NormalizedFullName);
            Assert.Equal("تأمین‌کننده جدید", receipt.Supplier?.Title);
            var product = Assert.Single(receipt.Items).Product;
            Assert.Equal(Product.CreateComparisonKey("کالای آزمایشی"), product.NormalizedTitle);
            Assert.Equal(Product.CreateComparisonKey("A B12"), product.NormalizedCode);
        }

        var duplicateTitle = await CreateReceiptErrorAsync(
            client,
            accessToken,
            null,
            "تحویل دهنده جدید",
            warehouseId,
            unitOfMeasureId,
            "کالای آزمایشی",
            "CD-34");
        Assert.Contains(duplicateTitle.Messages, message => message.Contains("عنوان کالا"));

        var duplicateCode = await CreateReceiptErrorAsync(
            client,
            accessToken,
            null,
            "تحویل دهنده جدید",
            warehouseId,
            unitOfMeasureId,
            "کالای دوم",
            "A B12");
        Assert.Contains(duplicateCode.Messages, message => message.Contains("کد کالا"));

        var unknownUnit = await CreateReceiptErrorAsync(
            client,
            accessToken,
            null,
            "تحویل دهنده جدید",
            warehouseId,
            Guid.NewGuid(),
            "کالای واحد نامعتبر",
            "UNIT-404");
        Assert.Contains(unknownUnit.Messages, message => message.Contains("واحد اندازه‌گیری"));

        await AssertPersonCreateFailsAsync(client, accessToken, "تحویل‌دهنده جدید");

        await AssertProductCreateFailsAsync(
            client,
            accessToken,
            "کالای آزمایشی",
            "UNIQUE-01",
            "عنوان کالا");
        await AssertProductCreateFailsAsync(
            client,
            accessToken,
            "کالای مستقل",
            "A B12",
            "کد کالا");

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await verificationDatabase.People.CountAsync());
        Assert.Equal(1, await verificationDatabase.UnitsOfMeasure.CountAsync());
    }

    private static async Task<(Guid WarehouseId, Guid UnitOfMeasureId)> SeedReferencesAsync(
        FullStackWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var warehouse = new StorageWarehouse("انبار تست", null);
        var unit = new UnitOfMeasure("عدد", null);
        database.Warehouses.Add(warehouse);
        database.UnitsOfMeasure.Add(unit);
        await database.SaveChangesAsync();
        return (warehouse.Id, unit.Id);
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/endpoint/auth/login",
            new
            {
                email = FullStackWebApplicationFactory.AdminEmail,
                password = FullStackWebApplicationFactory.AdminPassword
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<TokenData>>(JsonOptions);
        return Assert.IsType<TokenData>(
            Assert.IsType<ApiEnvelope<TokenData>>(envelope).Data).AccessToken;
    }

    private static async Task<GoodsReceiptData> CreateReceiptAsync(
        HttpClient client,
        string accessToken,
        Guid? delivererId,
        string? delivererFullName,
        Guid warehouseId,
        Guid unitOfMeasureId,
        string supplierTitle,
        string productTitle,
        string productCode,
        HttpStatusCode expectedStatus)
    {
        using var request = CreateRequest(
            accessToken,
            delivererId,
            delivererFullName,
            warehouseId,
            unitOfMeasureId,
            supplierTitle,
            productTitle,
            productCode);
        using var response = await client.SendAsync(request);
        await AssertStatusAsync(response, expectedStatus);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<GoodsReceiptData>>(JsonOptions);
        return Assert.IsType<GoodsReceiptData>(
            Assert.IsType<ApiEnvelope<GoodsReceiptData>>(envelope).Data);
    }

    private static async Task<ApiEnvelope<object>> CreateReceiptErrorAsync(
        HttpClient client,
        string accessToken,
        Guid? delivererId,
        string? delivererFullName,
        Guid warehouseId,
        Guid unitOfMeasureId,
        string productTitle,
        string productCode)
    {
        using var request = CreateRequest(
            accessToken,
            delivererId,
            delivererFullName,
            warehouseId,
            unitOfMeasureId,
            null,
            productTitle,
            productCode);
        using var response = await client.SendAsync(request);
        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
        return Assert.IsType<ApiEnvelope<object>>(
            await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>());
    }

    private static HttpRequestMessage CreateRequest(
        string accessToken,
        Guid? delivererId,
        string? delivererFullName,
        Guid warehouseId,
        Guid unitOfMeasureId,
        string? supplierTitle,
        string productTitle,
        string productCode)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/endpoint/inventory/goods-receipts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            enteredAtUtc = DateTimeOffset.UtcNow,
            description = (string?)null,
            delivererId,
            delivererFullName,
            warehouseId,
            invoiceStatus = "DoesNotHave",
            invoiceNumber = (string?)null,
            supplierId = (Guid?)null,
            supplierTitle,
            receiptNumber = (string?)null,
            pageNumber = Guid.NewGuid().ToString("N"),
            items = new[]
            {
                new
                {
                    productId = (Guid?)null,
                    productTitle,
                    productCode,
                    unitOfMeasureId,
                    quantity = 1m,
                    requestNumber = (string?)null,
                    description = (string?)null
                }
            }
        });
        return request;
    }

    private static async Task AssertPersonCreateFailsAsync(
        HttpClient client,
        string accessToken,
        string fullName)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/endpoint/master-data/people");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            fullName,
            phoneNumber = "09121111111"
        });

        using var response = await client.SendAsync(request);
        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
        var envelope = Assert.IsType<ApiEnvelope<object>>(
            await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions));
        Assert.Contains(envelope.Messages, message => message.Contains("نام تحویل‌دهنده"));
    }

    private static async Task AssertProductCreateFailsAsync(
        HttpClient client,
        string accessToken,
        string title,
        string code,
        string expectedMessage)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/endpoint/master-data/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            title,
            code,
            unitOfMeasureTitle = "عدد",
            description = (string?)null
        });

        using var response = await client.SendAsync(request);
        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
        var envelope = Assert.IsType<ApiEnvelope<object>>(
            await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(JsonOptions));
        Assert.Contains(envelope.Messages, message => message.Contains(expectedMessage));
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

    private sealed class GoodsReceiptData
    {
        public Guid Id { get; init; }

        public Guid DelivererId { get; init; }

        public string DelivererFullName { get; init; } = string.Empty;

        public Guid? SupplierId { get; init; }

        public string? Counterparty { get; init; }
    }
}
