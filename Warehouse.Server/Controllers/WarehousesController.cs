using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.Warehouses.Commands.CreateWarehouse;
using Warehouse.Application.Features.MasterData.Warehouses.Commands.DeleteWarehouse;
using Warehouse.Application.Features.MasterData.Warehouses.Commands.UpdateWarehouse;
using Warehouse.Application.Features.MasterData.Warehouses.Queries.GetWarehouseById;
using Warehouse.Application.Features.MasterData.Warehouses.Queries.GetWarehouseValidationRules;
using Warehouse.Application.Features.MasterData.Warehouses.Queries.GetWarehouses;
using Warehouse.Server.Contracts.MasterData;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/master-data/warehouses")]
public sealed class WarehousesController(ISender sender, IMapper mapper) : ControllerBase
{
    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet("validation-rules")]
    public async Task<IActionResult> GetValidationRules(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWarehouseValidationRulesQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet]
    [ProducesResponseType(typeof(Result<MasterDataListResult<TitledMasterDataSummary>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<MasterDataListResult<TitledMasterDataSummary>>>> GetAll(
        [FromQuery] GetWarehousesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<TitledMasterDataSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<TitledMasterDataSummary>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<TitledMasterDataSummary>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetWarehouseByIdQuery(id),
            cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPost]
    [ProducesResponseType(typeof(Result<TitledMasterDataSummary>), StatusCodes.Status201Created)]
    public async Task<ActionResult<Result<TitledMasterDataSummary>>> Create(
        [FromBody] TitledMasterDataRequest request,
        CancellationToken cancellationToken)
    {
        var command = mapper.Map<CreateWarehouseCommand>(request);
        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result<TitledMasterDataSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<TitledMasterDataSummary>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<TitledMasterDataSummary>>> Update(
        Guid id,
        [FromBody] TitledMasterDataRequest request,
        CancellationToken cancellationToken)
    {
        var command = mapper.Map<UpdateWarehouseCommand>(request) with { Id = id };
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteWarehouseCommand(id),
            cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }
}
