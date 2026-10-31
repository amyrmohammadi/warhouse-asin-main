using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Auditing;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceipts;

namespace Warehouse.Server.Filters;

public sealed class GoodsReceiptColumnAccessFilter(IAuthorizationService authorization) : IAsyncActionFilter
{
    public static async Task<GoodsReceiptColumnAccess> GetAccessAsync(
        IAuthorizationService authorization, System.Security.Claims.ClaimsPrincipal user)
    {
        async Task<bool> Has(string policy) => (await authorization.AuthorizeAsync(user, policy)).Succeeded;
        return new(
            await Has(PermissionPolicyNames.Inventory.GoodsReceiptsWarehouseRead),
            await Has(PermissionPolicyNames.Inventory.GoodsReceiptsInvoiceStatusRead),
            await Has(PermissionPolicyNames.Inventory.GoodsReceiptsCounterpartyRead),
            await Has(PermissionPolicyNames.Inventory.GoodsReceiptsInvoiceNumberRead),
            await Has(PermissionPolicyNames.Inventory.GoodsReceiptsReceiptNumberRead));
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var access = await GetAccessAsync(authorization, context.HttpContext.User);
        foreach (var key in context.ActionArguments.Keys.ToArray())
        {
            if (context.ActionArguments[key] is GetGoodsReceiptItemRowsQuery rows)
                context.ActionArguments[key] = rows with { ColumnAccess = access };
            if (context.ActionArguments[key] is GetGoodsReceiptsQuery receipts)
                context.ActionArguments[key] = receipts with { ColumnAccess = access };
        }
        var executed = await next();
        if (executed.Result is not ObjectResult result) return;
        result.Value = result.Value switch
        {
            Result<GoodsReceiptSummary> { IsSuccess: true, Data: { } data } value =>
                Result<GoodsReceiptSummary>.Success(access.Apply(data), value.Messages.ToArray()),
            Result<GoodsReceiptListResult> { IsSuccess: true, Data: { } data } value =>
                Result<GoodsReceiptListResult>.Success(data with { Items = data.Items.Select(access.Apply).ToArray() }, value.Messages.ToArray()),
            Result<GoodsReceiptItemRowListResult> { IsSuccess: true, Data: { } data } value =>
                Result<GoodsReceiptItemRowListResult>.Success(data with { Items = data.Items.Select(access.Apply).ToArray() }, value.Messages.ToArray()),
            Result<GoodsReceiptItemHistoryResult> { IsSuccess: true, Data: { } data } value =>
                Result<GoodsReceiptItemHistoryResult>.Success(access.Apply(data), value.Messages.ToArray()),
            Result<GoodsReceiptOptions> { IsSuccess: true, Data: { } data } =>
                Result<GoodsReceiptOptions>.Success(new GoodsReceiptOptions
                {
                    People = data.People, UnitsOfMeasure = data.UnitsOfMeasure,
                    Warehouses = access.Warehouse ? data.Warehouses : []
                }),
            _ => result.Value
        };
    }
}
