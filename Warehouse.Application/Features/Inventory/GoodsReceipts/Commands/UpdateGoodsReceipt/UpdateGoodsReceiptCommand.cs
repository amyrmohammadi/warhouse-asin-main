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
        if (!request.CanEditInitialEntry)
        {
            var repository = unitOfWork.Repository<GoodsReceiptItem>();
            var items = await repository.ToListAsync(repository.Entities.Where(i => i.GoodsReceiptId == receipt.Id), cancellationToken);
            values = values with
            {
                EnteredAtUtc = receipt.EnteredAtUtc, Description = receipt.Description,
                DelivererId = receipt.DelivererId, DelivererFullName = null,
                WarehouseId = receipt.WarehouseId, PageNumber = receipt.PageNumber,
                Items = items.Select(i => new CreateGoodsReceiptItem(i.ProductId, null, null, null,
                    i.Quantity, i.RequestNumber, i.Description)).ToArray()
            };
        }
        if (!request.CanEditReceiptInvoice)
            values = values with
            {
                InvoiceStatus = receipt.InvoiceStatus, InvoiceNumber = receipt.InvoiceNumber,
                SupplierId = receipt.SupplierId, SupplierTitle = null, ReceiptNumber = receipt.ReceiptNumber
            };
        await new CreateGoodsReceiptCommandValidator().ValidateAndThrowAsync(values, cancellationToken);
        return await new CreateGoodsReceiptCommandHandler(unitOfWork).SaveAsync(values, cancellationToken,
            receipt, request.CanEditInitialEntry, request.CanEditReceiptInvoice);
    }
}
