using System.Security.Claims;
using System.Reflection;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.Products.Queries.SearchProducts;
using Warehouse.Server.Controllers;

namespace Warehouse.Server.Tests.Controllers;

public sealed class ProductsControllerTests
{
    [Theory]
    [InlineData(nameof(ProductsController.Create))]
    [InlineData(nameof(ProductsController.Import))]
    public void ProductCreationActions_UseProductsCreatePolicy(string actionName)
    {
        var action = typeof(ProductsController).GetMethod(actionName)!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(PermissionPolicyNames.MasterData.ProductsCreate, authorize.Policy);
    }

    [Fact]
    public async Task Search_WithMasterDataPermission_ReturnsMatchingProducts()
    {
        var sender = Substitute.For<ISender>();
        var auth = Authorize(PermissionPolicyNames.MasterData.Read);
        var expected = new List<ProductSummary>
        {
            new() { Id = Guid.NewGuid(), Title = "کالای الف", Code = "A1" }
        };
        sender.Send(Arg.Any<SearchProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyCollection<ProductSummary>>.Success(expected));
        var controller = CreateController(sender, auth);

        var response = await controller.Search(new SearchProductsQuery { Search = "کالا" }, default);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<Result<IReadOnlyCollection<ProductSummary>>>(ok.Value);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        await sender.Received(1).Send(
            Arg.Is<SearchProductsQuery>(query => query.Search == "کالا"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_WithInventoryPermission_ReturnsMatchingProducts()
    {
        var sender = Substitute.For<ISender>();
        var auth = Authorize(PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry);
        sender.Send(Arg.Any<SearchProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyCollection<ProductSummary>>.Success([]));
        var controller = CreateController(sender, auth);

        var response = await controller.Search(new SearchProductsQuery { Search = "کالا" }, default);

        Assert.IsType<OkObjectResult>(response.Result);
        await sender.Received(1).Send(Arg.Any<SearchProductsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_WithProductsCreatePermission_ReturnsMatchingProducts()
    {
        var sender = Substitute.For<ISender>();
        var auth = Authorize(PermissionPolicyNames.MasterData.ProductsCreate);
        sender.Send(Arg.Any<SearchProductsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyCollection<ProductSummary>>.Success([]));
        var controller = CreateController(sender, auth);

        var response = await controller.Search(new SearchProductsQuery { Search = "کالا" }, default);

        Assert.IsType<OkObjectResult>(response.Result);
        await sender.Received(1).Send(Arg.Any<SearchProductsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_WithoutAnyPermission_IsForbidden()
    {
        var sender = Substitute.For<ISender>();
        var auth = Authorize();
        var controller = CreateController(sender, auth);

        var response = await controller.Search(new SearchProductsQuery { Search = "کالا" }, default);

        Assert.IsType<ForbidResult>(response.Result);
        await sender.DidNotReceive().Send(Arg.Any<SearchProductsQuery>(), Arg.Any<CancellationToken>());
    }

    private static ProductsController CreateController(ISender sender, IAuthorizationService auth) =>
        new(sender, Substitute.For<IMapper>(), auth)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static IAuthorizationService Authorize(params string[] grantedPolicies)
    {
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => grantedPolicies.Contains(call.Arg<string>())
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        return auth;
    }
}
