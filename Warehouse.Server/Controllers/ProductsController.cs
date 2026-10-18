using Warehouse.Application.Features.MasterData.Products.Commands.ImportProducts;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.Products.Commands.CreateProduct;
using Warehouse.Application.Features.MasterData.Products.Commands.DeleteProduct;
using Warehouse.Application.Features.MasterData.Products.Commands.UpdateProduct;
using Warehouse.Application.Features.MasterData.Products.Queries.GetProductById;
using Warehouse.Application.Features.MasterData.Products.Queries.GetProducts;
using Warehouse.Application.Features.MasterData.Products.Queries.GetProductValidationRules;
using Warehouse.Server.Contracts.MasterData;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/master-data/products")]
public sealed class ProductsController(ISender sender, IMapper mapper) : ControllerBase
{
    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet("validation-rules")]
    public async Task<IActionResult> GetValidationRules(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProductValidationRulesQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet]
    [ProducesResponseType(typeof(Result<MasterDataListResult<ProductSummary>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<MasterDataListResult<ProductSummary>>>> GetAll(
        [FromQuery] GetProductsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Read)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<ProductSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<ProductSummary>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<ProductSummary>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProductByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPost]
    [ProducesResponseType(typeof(Result<ProductSummary>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result<ProductSummary>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<ProductSummary>>> Create(
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(mapper.Map<CreateProductCommand>(request), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(51 * 1024 * 1024)]
    [ProducesResponseType(typeof(Result<ProductImportSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<ProductImportSummary>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<ProductImportSummary>>> Import(
        [FromForm] ImportProductsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return BadRequest(Result<ProductImportSummary>.Error(
                "انتخاب فایل Excel الزامی است."));
        }

        if (request.File.Length > ImportProductsCommand.MaxFileSizeBytes)
        {
            return BadRequest(Result<ProductImportSummary>.Error(
                "حجم فایل Excel نباید بیشتر از ۵۰ مگابایت باشد."));
        }

        await using var stream = new MemoryStream();
        await request.File.CopyToAsync(stream, cancellationToken);
        var command = new ImportProductsCommand(
            request.File.FileName,
            stream.ToArray(),
            request.CheckOnlyProductCode);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.MasterData.Manage)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result<ProductSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<ProductSummary>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<ProductSummary>>> Update(
        Guid id,
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken)
    {
        var command = mapper.Map<UpdateProductCommand>(request) with { Id = id };
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
        var result = await sender.Send(new DeleteProductCommand(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }
}
