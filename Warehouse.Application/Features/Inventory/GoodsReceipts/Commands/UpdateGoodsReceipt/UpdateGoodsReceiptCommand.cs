using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;

public sealed record UpdateGoodsReceiptCommand(Guid Id, CreateGoodsReceiptCommand Values,
    bool CanEditInitialEntry, bool CanEditInvoice, bool CanEditReceipt,
    Guid ConcurrencyStamp = default, bool CanEditWarehouse = false)
    : IRequest<Result<GoodsReceiptSummary>>;

internal sealed class UpdateGoodsReceiptCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdateGoodsReceiptCommand, Result<GoodsReceiptSummary>>
{
    public async Task<Result<GoodsReceiptSummary>> Handle(UpdateGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        if (!request.CanEditInitialEntry && !request.CanEditInvoice && !request.CanEditReceipt && !request.CanEditWarehouse)
            return Result<GoodsReceiptSummary>.Error("مجوز ویرایش سند را ندارید.");
        var receipt = await unitOfWork.Repository<GoodsReceipt>()
                      .GetByIdAsync(request.Id, cancellationToken);
        if (receipt is null) return Result<GoodsReceiptSummary>
                      .Error("سند ورود کالا پیدا نشد.");
        if (await Invoices.InvoiceEditGuard.IsReceiptLockedAsync(unitOfWork, receipt.Id, cancellationToken))
            return Result<GoodsReceiptSummary>.Error(Invoice.LockedMessage);
        if (!receipt.TryAdvanceConcurrencyStamp(request.ConcurrencyStamp))
            return Result<GoodsReceiptSummary>.Error(ConcurrencyMessages.Conflict);

        var values = request.Values;
        var repository = unitOfWork.Repository<GoodsReceiptItem>();
        var storedItems = await repository.ToListAsync(
            repository.Entities
                .Where(item => item.GoodsReceiptId == receipt.Id)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id),
            cancellationToken);
        var storedInvoiceIds = storedItems
            .Where(item => item.InvoiceId.HasValue)
            .Select(item => item.InvoiceId!.Value)
            .Distinct()
            .ToArray();
        var invoiceRepository = unitOfWork.Repository<Invoice>();
        var storedInvoices = await invoiceRepository.ToListAsync(
            invoiceRepository.Entities.Where(invoice => storedInvoiceIds.Contains(invoice.Id)),
            cancellationToken);
        var invoicesById = storedInvoices.ToDictionary(invoice => invoice.Id);
        if (!request.CanEditInitialEntry)
        {
            var requestedByProduct = values.Items
                .Where(item => item.ProductId.HasValue)
                .GroupBy(item => item.ProductId!.Value)
                .ToDictionary(group => group.Key, group => new Queue<CreateGoodsReceiptItem>(group));
            values = values with
            {
                EnteredAtUtc = receipt.EnteredAtUtc,
                Description = receipt.Description,
                DelivererId = receipt.DelivererId,
                DelivererFullName = null,
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
                        item.ProductId, null, null, null,
                        request.CanEditWarehouse ? requested?.WarehouseId ?? item.WarehouseId : item.WarehouseId,
                        request.CanEditInvoice ? requested?.InvoiceStatus : item.InvoiceStatus,
                        request.CanEditInvoice ? requested?.InvoiceNumber :
                            item.InvoiceId.HasValue ? invoicesById[item.InvoiceId.Value].InvoiceNumber : null,
                        request.CanEditInvoice ? requested?.SupplierId :
                            item.InvoiceId.HasValue ? invoicesById[item.InvoiceId.Value].SupplierId : item.SupplierId,
                        request.CanEditInvoice ? requested?.SupplierTitle : null,
                        request.CanEditReceipt ? requested?.ReceiptNumber : item.ReceiptNumber,
                        item.Quantity, item.RequestNumber, item.Description);
                }).ToArray()
            };
        }
        else if (!request.CanEditInvoice || !request.CanEditReceipt)
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
                        InvoiceStatus = request.CanEditInvoice ? item.InvoiceStatus : stored?.InvoiceStatus,
                        InvoiceNumber = request.CanEditInvoice ? item.InvoiceNumber : stored?.InvoiceId is Guid invoiceId
                            ? invoicesById[invoiceId].InvoiceNumber
                            : null,
                        SupplierId = request.CanEditInvoice ? item.SupplierId : stored?.InvoiceId is Guid supplierInvoiceId
                            ? invoicesById[supplierInvoiceId].SupplierId
                            : stored?.SupplierId,
                        SupplierTitle = request.CanEditInvoice ? item.SupplierTitle : null,
                        ReceiptNumber = request.CanEditReceipt ? item.ReceiptNumber : stored?.ReceiptNumber
                    };
                }).ToArray()
            };
        }
        // Preserve warehouses on existing rows even when the caller can edit initial-entry details.
        if (!request.CanEditWarehouse)
        {
            var warehousesByProduct = storedItems.GroupBy(item => item.ProductId)
                .ToDictionary(group => group.Key, group => new Queue<GoodsReceiptItem>(group));
            values = values with
            {
                Items = values.Items.Select(item =>
                {
                    if (item.ProductId is Guid productId && warehousesByProduct.TryGetValue(productId, out var stored) && stored.Count > 0)
                        return item with { WarehouseId = stored.Dequeue().WarehouseId };
                    return item;
                }).ToArray()
            };
        }
        await new CreateGoodsReceiptCommandValidator().ValidateAndThrowAsync(values, cancellationToken);
        return await new CreateGoodsReceiptCommandHandler(unitOfWork).SaveAsync(
            values,
            cancellationToken,
            existing: receipt,
            updateInitial: request.CanEditInitialEntry,
            updateInvoice: request.CanEditInvoice,
            updateReceipt: request.CanEditReceipt,
            updateWarehouse: request.CanEditWarehouse);
    }
}
