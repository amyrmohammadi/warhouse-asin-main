using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Commands.CreateGoodsIssueDestination;
using Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Commands.DeleteGoodsIssueDestination;
using Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Commands.UpdateGoodsIssueDestination;
using Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Queries.GetGoodsIssueDestinationById;
using Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Queries.GetGoodsIssueDestinations;
using Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Queries.GetGoodsIssueDestinationValidationRules;
using Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Queries.SearchGoodsIssueDestinations;
using Warehouse.Server.Contracts.MasterData;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/master-data/goods-issue-destinations")]
public sealed class GoodsIssueDestinationsController(
    ISender sender,
    IAuthorizationService authorizationService) : ControllerBase
{
    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet("validation-rules")]
    public async Task<IActionResult> GetValidationRules(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetGoodsIssueDestinationValidationRulesQuery(), cancellationToken));

    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet]
    public async Task<ActionResult<Result<MasterDataListResult<TitledMasterDataSummary>>>> GetAll(
        [FromQuery] GetGoodsIssueDestinationsQuery query,
        CancellationToken cancellationToken) => Ok(await sender.Send(query, cancellationToken));

    [Authorize]
    [HttpGet("lookup")]
    public async Task<ActionResult<Result<IReadOnlyCollection<TitledMasterDataSummary>>>> Search(
        [FromQuery] SearchGoodsIssueDestinationsQuery query,
        CancellationToken cancellationToken)
    {
        if (!await CanReadDestinationsAsync()) return Forbid();
        return Ok(await sender.Send(query, cancellationToken));
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<TitledMasterDataSummary>>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        if (!await CanReadDestinationsAsync()) return Forbid();
        var result = await sender.Send(new GetGoodsIssueDestinationByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPost]
    public async Task<ActionResult<Result<TitledMasterDataSummary>>> Create(
        [FromBody] TitledMasterDataRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateGoodsIssueDestinationCommand(request.Title, request.Description), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Result<TitledMasterDataSummary>>> Update(
        Guid id, [FromBody] TitledMasterDataRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateGoodsIssueDestinationCommand(id, request.Title, request.Description), cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Result>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteGoodsIssueDestinationCommand(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    private async Task<bool> CanReadDestinationsAsync()
    {
        var policies = new[]
        {
            PermissionPolicyNames.MasterData.Read,
            PermissionPolicyNames.Inventory.GoodsIssuesRead,
            PermissionPolicyNames.Inventory.GoodsIssuesManage
        };
        foreach (var policy in policies)
            if ((await authorizationService.AuthorizeAsync(User, policy)).Succeeded) return true;
        return false;
    }
}
