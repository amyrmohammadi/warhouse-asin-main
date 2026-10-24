using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.CreateUnitOfMeasure;
using Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.DeleteUnitOfMeasure;
using Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.UpdateUnitOfMeasure;
using Warehouse.Application.Features.MasterData.UnitsOfMeasure.Queries.GetUnitOfMeasureById;
using Warehouse.Application.Features.MasterData.UnitsOfMeasure.Queries.GetUnitOfMeasureValidationRules;
using Warehouse.Application.Features.MasterData.UnitsOfMeasure.Queries.GetUnitsOfMeasure;
using Warehouse.Server.Contracts.MasterData;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/master-data/units-of-measure")]
public sealed class UnitsOfMeasureController(ISender sender, IMapper mapper) : ControllerBase
{
    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet("validation-rules")]
    public async Task<IActionResult> GetValidationRules(CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetUnitOfMeasureValidationRulesQuery(),
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet]
    [ProducesResponseType(typeof(Result<MasterDataListResult<TitledMasterDataSummary>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<MasterDataListResult<TitledMasterDataSummary>>>> GetAll(
        [FromQuery] GetUnitsOfMeasureQuery query,
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
            new GetUnitOfMeasureByIdQuery(id),
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
        var command = mapper.Map<CreateUnitOfMeasureCommand>(request);
        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result<TitledMasterDataSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<TitledMasterDataSummary>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<TitledMasterDataSummary>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Result<TitledMasterDataSummary>>> Update(
        Guid id,
        [FromBody] TitledMasterDataRequest request,
        CancellationToken cancellationToken)
    {
        var command = mapper.Map<UpdateUnitOfMeasureCommand>(request) with { Id = id };
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Ok(result)
            : result.Messages.Contains(ConcurrencyMessages.Conflict, StringComparer.Ordinal)
                ? Conflict(result)
                : NotFound(result);
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
            new DeleteUnitOfMeasureCommand(id),
            cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }
}
