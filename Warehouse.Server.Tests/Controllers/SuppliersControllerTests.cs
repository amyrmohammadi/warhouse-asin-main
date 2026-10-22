using System.Security.Claims;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.Suppliers.Queries.SearchSuppliers;
using Warehouse.Server.Controllers;

namespace Warehouse.Server.Tests.Controllers;

public sealed class SuppliersControllerTests
{
    [Fact]
    public async Task Search_WithMasterDataPermission_ReturnsMatchingSuppliers()
    {
        var sender = Substitute.For<ISender>();
        var auth = Authorize(PermissionPolicyNames.MasterData.Read);
        var expected = new List<TitledMasterDataSummary>
        {
            new() { Id = Guid.NewGuid(), Title = "تأمین‌کننده الف" }
        };
        sender.Send(Arg.Any<SearchSuppliersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyCollection<TitledMasterDataSummary>>.Success(expected));
        var controller = CreateController(sender, auth);

        var response = await controller.Search(new SearchSuppliersQuery { Search = "تأمین" }, default);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<Result<IReadOnlyCollection<TitledMasterDataSummary>>>(ok.Value);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        await sender.Received(1).Send(
            Arg.Is<SearchSuppliersQuery>(query => query.Search == "تأمین"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_WithInventoryPermission_ReturnsMatchingSuppliers()
    {
        var sender = Substitute.For<ISender>();
        var auth = Authorize(PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry);
        sender.Send(Arg.Any<SearchSuppliersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyCollection<TitledMasterDataSummary>>.Success([]));
        var controller = CreateController(sender, auth);

        var response = await controller.Search(new SearchSuppliersQuery { Search = "تأمین" }, default);

        Assert.IsType<OkObjectResult>(response.Result);
        await sender.Received(1).Send(Arg.Any<SearchSuppliersQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_WithoutAnyPermission_IsForbidden()
    {
        var sender = Substitute.For<ISender>();
        var auth = Authorize();
        var controller = CreateController(sender, auth);

        var response = await controller.Search(new SearchSuppliersQuery { Search = "تأمین" }, default);

        Assert.IsType<ForbidResult>(response.Result);
        await sender.DidNotReceive().Send(Arg.Any<SearchSuppliersQuery>(), Arg.Any<CancellationToken>());
    }

    private static SuppliersController CreateController(ISender sender, IAuthorizationService auth) =>
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
