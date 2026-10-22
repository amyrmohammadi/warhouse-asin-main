using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.Suppliers.Commands.CreateSupplier;
using Warehouse.Application.Features.MasterData.Suppliers.Commands.DeleteSupplier;
using Warehouse.Application.Features.MasterData.Suppliers.Commands.ImportSuppliers;
using Warehouse.Application.Features.MasterData.Suppliers.Commands.UpdateSupplier;
using Warehouse.Application.Features.MasterData.Suppliers.Queries.GetSupplierById;
using Warehouse.Application.Features.MasterData.Suppliers.Queries.GetSupplierValidationRules;
using Warehouse.Application.Features.MasterData.Suppliers.Queries.GetSuppliers;
using Warehouse.Application.Features.MasterData.Suppliers.Queries.SearchSuppliers;
using Warehouse.Server.Contracts.MasterData;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/master-data/suppliers")]
public sealed class SuppliersController(
    ISender sender,
    IMapper mapper,
    IAuthorizationService authorizationService) : ControllerBase
{
    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet("validation-rules")]
    public async Task<IActionResult> GetValidationRules(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSupplierValidationRulesQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(Result<MasterDataListResult<TitledMasterDataSummary>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<MasterDataListResult<TitledMasterDataSummary>>>> GetAll(
        [FromQuery] GetSuppliersQuery query,
        CancellationToken cancellationToken)
    {
        if (!await CanReadSuppliersAsync()) return Forbid();

        var result = await sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("lookup")]
    [ProducesResponseType(typeof(Result<IReadOnlyCollection<TitledMasterDataSummary>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<IReadOnlyCollection<TitledMasterDataSummary>>>> Search(
        [FromQuery] SearchSuppliersQuery query,
        CancellationToken cancellationToken)
    {
        if (!await CanReadSuppliersAsync()) return Forbid();

        var result = await sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<TitledMasterDataSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<TitledMasterDataSummary>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<TitledMasterDataSummary>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!await CanReadSuppliersAsync()) return Forbid();

        var result = await sender.Send(
            new GetSupplierByIdQuery(id),
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
        var command = mapper.Map<CreateSupplierCommand>(request);
        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType(typeof(Result<SupplierImportSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<SupplierImportSummary>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<SupplierImportSummary>>> Import(
        [FromForm] ImportSuppliersRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return BadRequest(Result<SupplierImportSummary>.Error(
                "انتخاب فایل Excel الزامی است."));
        }

        if (request.File.Length > ImportSuppliersCommand.MaxFileSizeBytes)
        {
            return BadRequest(Result<SupplierImportSummary>.Error(
                "حجم فایل Excel نباید بیشتر از ۱۰ مگابایت باشد."));
        }

        await using var stream = new MemoryStream();
        await request.File.CopyToAsync(stream, cancellationToken);
        var command = new ImportSuppliersCommand(
            request.File.FileName,
            stream.ToArray());
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
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
        var command = mapper.Map<UpdateSupplierCommand>(request) with { Id = id };
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
            new DeleteSupplierCommand(id),
            cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    private async Task<bool> CanReadSuppliersAsync()
    {
        var policies = new[]
        {
            PermissionPolicyNames.MasterData.Read,
            PermissionPolicyNames.Inventory.GoodsReceiptsRead,
            PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry,
            PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations
        };

        foreach (var policy in policies)
        {
            if ((await authorizationService.AuthorizeAsync(User, policy)).Succeeded) return true;
        }

        return false;
    }
}
