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
    public async Task Create_AllowsRepeatedProduct_WhenRowsDiffer_AndRejectsExactDuplicates()
    {
        using var factory = new FullStackWebApplicationFactory();
        await factory.InitializeDatabaseAsync();
        Guid warehouseId;
        Guid personId;
        Guid productId;
        Guid unitId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var warehouse = new StorageWarehouse("Warehouse", null);
            var person = new Person("Deliverer", "");
            var unit = new UnitOfMeasure("Unit", null);
            var product = new Product("Product", "P-001", unit.Id, null);
            database.AddRange(warehouse, person, unit, product);
            await database.SaveChangesAsync();
            warehouseId = warehouse.Id;
            personId = person.Id;
            productId = product.Id;
            unitId = unit.Id;
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var accessToken = await LoginAsync(client);

        object Item(decimal quantity) => new
        {
            productId,
            productTitle = (string?)null,
            productCode = (string?)null,
            unitOfMeasureId = (Guid?)null,
            warehouseId,
            invoiceStatus = (string?)null,
            invoiceNumber = (string?)null,
            supplierId = (Guid?)null,
            supplierTitle = (string?)null,
            receiptNumber = (string?)null,
            quantity,
            requestNumber = (string?)null,
            description = (string?)null
        };

        HttpRequestMessage Request(object[] items)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/endpoint/inventory/goods-receipts");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new
            {
                enteredAtUtc = DateTimeOffset.UtcNow,
                description = (string?)null,
                delivererId = personId,
                delivererFullName = (string?)null,
                pageNumber = Guid.NewGuid().ToString("N"),
                items
            });
            return request;
        }

        using (var request = Request([Item(1), Item(2)]))
        using (var response = await client.SendAsync(request))
        {
            await AssertStatusAsync(response, HttpStatusCode.Created);
            var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<GoodsReceiptData>>(JsonOptions);
            Assert.Equal(2, Assert.IsType<GoodsReceiptData>(
                Assert.IsType<ApiEnvelope<GoodsReceiptData>>(envelope).Data).Items.Count);
        }

        using (var request = Request([Item(1), Item(1)]))
        using (var response = await client.SendAsync(request))
        {
            await AssertStatusAsync(response, HttpStatusCode.BadRequest);
        }

        object NewItem(decimal quantity) => new
        {
            productId = (Guid?)null,
            productTitle = "New Product",
            productCode = "N-001",
            unitOfMeasureId = (Guid?)unitId,
            warehouseId,
            invoiceStatus = (string?)null,
            invoiceNumber = (string?)null,
            supplierId = (Guid?)null,
            supplierTitle = (string?)null,
            receiptNumber = (string?)null,
            quantity,
            requestNumber = (string?)null,
            description = (string?)null
        };

        using (var request = Request([NewItem(1), NewItem(2)]))
        using (var response = await client.SendAsync(request))
        {
            await AssertStatusAsync(response, HttpStatusCode.Created);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await database.Products.CountAsync(product => product.Code == "N-001"));
            var repeatedProduct = await database.Products.SingleAsync(product => product.Code == "N-001");
            Assert.Equal(2, await database.GoodsReceiptItems.CountAsync(
                item => item.ProductId == repeatedProduct.Id));
        }
    }

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
                .Include(item => item.Items)
                    .ThenInclude(item => item.Invoice)
                        .ThenInclude(invoice => invoice!.Supplier)
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
            pageNumber = Guid.NewGuid().ToString("N"),
            items = new[]
            {
                new
                {
                    productId = (Guid?)null,
                    productTitle,
                    productCode,
                    unitOfMeasureId,
                    warehouseId,
                    invoiceStatus = "HasInvoice",
                    invoiceNumber = $"INV-{productCode}",
                    supplierId = (Guid?)null,
                    supplierTitle,
                    receiptNumber = (string?)null,
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

        public IReadOnlyCollection<GoodsReceiptItemData> Items { get; init; } = [];

        public Guid? SupplierId => Items.FirstOrDefault()?.SupplierId;

        public string? Counterparty => Items.FirstOrDefault()?.Counterparty;
    }

    private sealed class GoodsReceiptItemData
    {
        public Guid? SupplierId { get; init; }

        public string? Counterparty { get; init; }
    }
}
