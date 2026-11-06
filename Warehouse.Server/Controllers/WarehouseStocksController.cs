using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Inventory.WarehouseStocks;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/inventory")]
public sealed class WarehouseStocksController(ISender sender) : ControllerBase
{
    [HttpGet("warehouses/{warehouseId:guid}/stocks/{productId:guid}/history")]
    [Authorize(Policy = PermissionPolicyNames.Inventory.WarehouseStockView)]
    public async Task<ActionResult<Result<StockHistoryPage>>> GetHistory(Guid warehouseId, Guid productId,
        CancellationToken token, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(new GetStockHistoryQuery(warehouseId, productId, pageNumber, pageSize), token);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("warehouses/{warehouseId:guid}/stocks")]
    [Authorize(Policy = PermissionPolicyNames.Inventory.WarehouseStockView)]
    public async Task<ActionResult<Result<WarehouseStockList>>> GetStocks(Guid warehouseId,
        [FromQuery] GetWarehouseStocksQuery query, CancellationToken token)
    {
        var result = await sender.Send(query with { WarehouseId = warehouseId }, token);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("opening-balances")]
    [Authorize(Policy = PermissionPolicyNames.Inventory.WarehouseOpeningBalanceManage)]
    public async Task<ActionResult<Result>> OpeningBalances([FromBody] RegisterOpeningBalancesCommand request, CancellationToken token) =>
        Respond(await sender.Send(request, token));

    [HttpPost("adjustments")]
    [Authorize(Policy = PermissionPolicyNames.Inventory.WarehouseInventoryAdjustmentManage)]
    public async Task<ActionResult<Result>> Adjust([FromBody] AdjustWarehouseStockCommand request, CancellationToken token) =>
        Respond(await sender.Send(request, token));

    private ActionResult<Result> Respond(Result result) => result.IsSuccess ? Ok(result)
        : result.Messages.Contains(ConcurrencyMessages.Conflict, StringComparer.Ordinal) ? Conflict(result) : BadRequest(result);
}
