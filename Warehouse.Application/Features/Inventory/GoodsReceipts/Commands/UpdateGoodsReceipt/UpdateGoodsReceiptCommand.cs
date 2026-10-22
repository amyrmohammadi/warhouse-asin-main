using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;

public sealed record UpdateGoodsReceiptCommand(Guid Id, CreateGoodsReceiptCommand Values,
    bool CanEditInitialEntry, bool CanEditReceiptInvoice) : IRequest<Result<GoodsReceiptSummary>>;

internal sealed class UpdateGoodsReceiptCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdateGoodsReceiptCommand, Result<GoodsReceiptSummary>>
{
    public async Task<Result<GoodsReceiptSummary>> Handle(UpdateGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        if (!request.CanEditInitialEntry && !request.CanEditReceiptInvoice)
            return Result<GoodsReceiptSummary>.Error("مجوز ویرایش سند را ندارید.");
        var receipt = await unitOfWork.Repository<GoodsReceipt>().GetByIdAsync(request.Id, cancellationToken);
        if (receipt is null) return Result<GoodsReceiptSummary>.Error("سند ورود کالا پیدا نشد.");
        var values = request.Values;
        var repository = unitOfWork.Repository<GoodsReceiptItem>();
        var storedItems = await repository.ToListAsync(
            repository.Entities
                .Where(item => item.GoodsReceiptId == receipt.Id)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id),
            cancellationToken);
        if (!request.CanEditInitialEntry)
        {
            var requestedByProduct = values.Items
                .Where(item => item.ProductId.HasValue)
                .GroupBy(item => item.ProductId!.Value)
                .ToDictionary(group => group.Key, group => new Queue<CreateGoodsReceiptItem>(group));
            values = values with
            {
                EnteredAtUtc = receipt.EnteredAtUtc, Description = receipt.Description,
                DelivererId = receipt.DelivererId, DelivererFullName = null,
                PageNumber = receipt.PageNumber,
                Items = storedItems.Select(item =>
                {
                    CreateGoodsReceiptItem? requested = null;
                    if (requestedByProduct.TryGetValue(item.ProductId, out var candidates) &&
                        candidates.Count > 0)
                    {
                        requested = candidates.Dequeue();
                    }
                    return new CreateGoodsReceiptItem(
                        item.ProductId, null, null, null, item.WarehouseId,
                        request.CanEditReceiptInvoice ? requested?.InvoiceStatus : item.InvoiceStatus,
                        request.CanEditReceiptInvoice ? requested?.InvoiceNumber : item.InvoiceNumber,
                        request.CanEditReceiptInvoice ? requested?.SupplierId : item.SupplierId,
                        request.CanEditReceiptInvoice ? requested?.SupplierTitle : null,
                        request.CanEditReceiptInvoice ? requested?.ReceiptNumber : item.ReceiptNumber,
                        item.Quantity, item.RequestNumber, item.Description);
                }).ToArray()
            };
        }
        else if (!request.CanEditReceiptInvoice)
        {
            var storedByProduct = storedItems
                .GroupBy(item => item.ProductId)
                .ToDictionary(group => group.Key, group => new Queue<GoodsReceiptItem>(group));
            values = values with
            {
                Items = values.Items.Select(item =>
                {
                    GoodsReceiptItem? stored = null;
                    if (item.ProductId.HasValue &&
                        storedByProduct.TryGetValue(item.ProductId.Value, out var candidates) &&
                        candidates.Count > 0)
                    {
                        stored = candidates.Dequeue();
                    }
                    return item with
                    {
                        InvoiceStatus = stored?.InvoiceStatus,
                        InvoiceNumber = stored?.InvoiceNumber,
                        SupplierId = stored?.SupplierId,
                        SupplierTitle = null,
                        ReceiptNumber = stored?.ReceiptNumber
                    };
                }).ToArray()
            };
        }
        await new CreateGoodsReceiptCommandValidator().ValidateAndThrowAsync(values, cancellationToken);
        return await new CreateGoodsReceiptCommandHandler(unitOfWork).SaveAsync(values, cancellationToken,
            receipt, request.CanEditInitialEntry, request.CanEditReceiptInvoice);
    }
}
