using System.Net;
using System.Net.Http.Json;
using MediatR;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Inventory.WarehouseStocks;

namespace Warehouse.Server.Tests.Integration;

public sealed class WarehouseStockPermissionTests
{
    [Theory]
    [InlineData("GET", "/endpoint/inventory/warehouses/11111111-1111-1111-1111-111111111111/stocks")]
    [InlineData("GET", "/endpoint/master-data/warehouses/options")]
    [InlineData("POST", "/endpoint/inventory/opening-balances")]
    [InlineData("POST", "/endpoint/inventory/adjustments")]
    public async Task DirectEndpoints_RejectUnrelatedPermissions(string method, string path)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add(TestAuthenticationDefaults.UserHeader, "true");
        request.Headers.Add(TestAuthenticationDefaults.PermissionsHeader, PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry);
        if (method == "POST") request.Content = JsonContent.Create(new { warehouseId = Guid.NewGuid(), items = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        Assert.Empty(factory.Sender.ReceivedCalls());
    }

    [Theory]
    [InlineData("/endpoint/inventory/opening-balances")]
    [InlineData("/endpoint/inventory/adjustments")]
    public async Task ViewPermission_DoesNotGrantWriteAccess(string path)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { warehouseId = Guid.NewGuid(), items = Array.Empty<object>() }) };
        request.Headers.Add(TestAuthenticationDefaults.UserHeader, "true");
        request.Headers.Add(TestAuthenticationDefaults.PermissionsHeader, PermissionPolicyNames.Inventory.WarehouseStockView);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        Assert.Empty(factory.Sender.ReceivedCalls());
    }

    [Fact]
    public async Task ViewPermission_CanReadStocksWithoutGoodsReceiptOrMasterDataPermissions()
    {
        await using var factory = new TestWebApplicationFactory();
        factory.Sender.Send(Arg.Any<GetWarehouseStocksQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<WarehouseStockList>.Success(new([], 0, 0)));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/endpoint/inventory/warehouses/{Guid.NewGuid()}/stocks");
        request.Headers.Add(TestAuthenticationDefaults.UserHeader, "true");
        request.Headers.Add(TestAuthenticationDefaults.PermissionsHeader, PermissionPolicyNames.Inventory.WarehouseStockView);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EachWritePermission_OnlyGrantsItsOwnOperation(bool opening)
    {
        await using var factory = new TestWebApplicationFactory();
        factory.Sender.Send(Arg.Any<RegisterOpeningBalancesCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        factory.Sender.Send(Arg.Any<AdjustWarehouseStockCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationDefaults.UserHeader, "true");
        client.DefaultRequestHeaders.Add(TestAuthenticationDefaults.PermissionsHeader, opening
            ? PermissionPolicyNames.Inventory.WarehouseOpeningBalanceManage : PermissionPolicyNames.Inventory.WarehouseInventoryAdjustmentManage);
        var own = opening ? "opening-balances" : "adjustments";
        var other = opening ? "adjustments" : "opening-balances";
        var body = new { warehouseId = Guid.NewGuid(), productId = Guid.NewGuid(), items = Array.Empty<object>(), reason = "علت" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/endpoint/inventory/{own}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/endpoint/inventory/{other}", body)).StatusCode);
    }
}
