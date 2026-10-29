using System.Reflection;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.SaveGoodsIssue;
using Warehouse.Server.Contracts.Inventory;
using Warehouse.Server.Controllers;

namespace Warehouse.Server.Tests.Controllers;

public sealed class GoodsIssuesControllerTests
{
    [Theory]
    [InlineData(nameof(GoodsIssuesController.Create), PermissionPolicyNames.Inventory.GoodsIssuesCreate)]
    [InlineData(nameof(GoodsIssuesController.Update), PermissionPolicyNames.Inventory.GoodsIssuesUpdate)]
    [InlineData(nameof(GoodsIssuesController.Delete), PermissionPolicyNames.Inventory.GoodsIssuesDelete)]
    [InlineData(nameof(GoodsIssuesController.Import), PermissionPolicyNames.Inventory.GoodsIssuesImport)]
    [InlineData(nameof(GoodsIssuesController.FinalizeDocuments), PermissionPolicyNames.Inventory.GoodsIssuesFinalize)]
    [InlineData(nameof(GoodsIssuesController.ReopenDocuments), PermissionPolicyNames.Inventory.GoodsIssuesReopen)]
    public void WriteActions_UseDedicatedPolicies(string actionName, string expectedPolicy)
    {
        var action = typeof(GoodsIssuesController).GetMethod(actionName)!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(expectedPolicy, authorize.Policy);
    }

    [Fact]
    public async Task Update_WithConcurrencyConflict_ReturnsHttp409()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<SaveGoodsIssueCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsIssueSummary>.Error(ConcurrencyMessages.Conflict));
        var controller = new GoodsIssuesController(
            sender,
            Substitute.For<IAuthorizationService>());
        var request = new SaveGoodsIssueRequest(
            "OUT-1", DateTimeOffset.UtcNow, null, Guid.NewGuid(), null,
            [new SaveGoodsIssueItemRequest(
                Guid.NewGuid(), "P-1", 1, null, Guid.NewGuid(), null,
                Domain.Enums.GoodsIssueExitReason.Consignment, null, "واحد", null)],
            Guid.NewGuid());

        var response = await controller.Update(Guid.NewGuid(), request, default);

        Assert.IsType<ConflictObjectResult>(response.Result);
    }

    [Fact]
    public async Task Create_WithUnselectedProductAndOnlyMasterDataManage_IsForbidden()
    {
        var sender = Substitute.For<ISender>();
        var controller = new GoodsIssuesController(
            sender,
            Authorize(PermissionPolicyNames.MasterData.Manage));
        var request = Request(productId: null);

        var response = await controller.Create(request, default);

        Assert.IsType<ForbidResult>(response.Result);
        await sender.DidNotReceive().Send(
            Arg.Any<SaveGoodsIssueCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithUnselectedProductAndProductsCreate_IsAllowed()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<SaveGoodsIssueCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsIssueSummary>.Success(new GoodsIssueSummary
            {
                Id = Guid.NewGuid(),
                IssueNumber = "OUT-1"
            }));
        var controller = new GoodsIssuesController(
            sender,
            Authorize(PermissionPolicyNames.MasterData.ProductsCreate));

        var unitId = Guid.NewGuid();
        var response = await controller.Create(
            Request(productId: null, productTitle: "کالای جدید", unitOfMeasureId: unitId), default);

        Assert.IsType<CreatedAtActionResult>(response.Result);
        await sender.Received(1).Send(
            Arg.Is<SaveGoodsIssueCommand>(command =>
                command.Items.Single().ProductId == null &&
                command.Items.Single().ProductTitle == "کالای جدید" &&
                command.Items.Single().UnitOfMeasureId == unitId),
            Arg.Any<CancellationToken>());
    }

    private static SaveGoodsIssueRequest Request(
        Guid? productId,
        string? productTitle = null,
        Guid? unitOfMeasureId = null) => new(
        "OUT-1", DateTimeOffset.UtcNow, null, Guid.NewGuid(), null,
        [new SaveGoodsIssueItemRequest(
            productId, "P-1", 1, null, Guid.NewGuid(), null,
            Domain.Enums.GoodsIssueExitReason.Consignment, null, null, null, productTitle, unitOfMeasureId)]);

    private static IAuthorizationService Authorize(params string[] grantedPolicies)
    {
        var authorizationService = Substitute.For<IAuthorizationService>();
        authorizationService.AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<object?>(),
                Arg.Any<string>())
            .Returns(call => grantedPolicies.Contains(call.Arg<string>())
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        return authorizationService;
    }
}
