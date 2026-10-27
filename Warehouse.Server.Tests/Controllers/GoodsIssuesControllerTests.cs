using System.Reflection;
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
                "علت خروج", null, "واحد", null)],
            Guid.NewGuid());

        var response = await controller.Update(Guid.NewGuid(), request, default);

        Assert.IsType<ConflictObjectResult>(response.Result);
    }
}
