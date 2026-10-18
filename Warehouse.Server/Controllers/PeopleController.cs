using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.People.Commands.CreatePerson;
using Warehouse.Application.Features.MasterData.People.Commands.DeletePerson;
using Warehouse.Application.Features.MasterData.People.Commands.UpdatePerson;
using Warehouse.Application.Features.MasterData.People.Queries.GetPeople;
using Warehouse.Application.Features.MasterData.People.Queries.GetPersonById;
using Warehouse.Application.Features.MasterData.People.Queries.GetPersonValidationRules;
using Warehouse.Server.Contracts.MasterData;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/master-data/people")]
public sealed class PeopleController(ISender sender, IMapper mapper) : ControllerBase
{
    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet("validation-rules")]
    public async Task<IActionResult> GetValidationRules(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPersonValidationRulesQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet]
    [ProducesResponseType(typeof(Result<MasterDataListResult<PersonSummary>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<MasterDataListResult<PersonSummary>>>> GetAll(
        [FromQuery] GetPeopleQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<PersonSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<PersonSummary>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<PersonSummary>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPersonByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPost]
    [ProducesResponseType(typeof(Result<PersonSummary>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result<PersonSummary>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<PersonSummary>>> Create(
        [FromBody] PersonRequest request,
        CancellationToken cancellationToken)
    {
        var command = mapper.Map<CreatePersonCommand>(request);
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result<PersonSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<PersonSummary>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<PersonSummary>>> Update(
        Guid id,
        [FromBody] PersonRequest request,
        CancellationToken cancellationToken)
    {
        var command = mapper.Map<UpdatePersonCommand>(request) with { Id = id };
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeletePersonCommand(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }
}
