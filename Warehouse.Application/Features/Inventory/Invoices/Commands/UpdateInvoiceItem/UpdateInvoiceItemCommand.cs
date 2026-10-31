using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoiceItem;

public sealed record UpdateInvoiceItemCommand(
    Guid InvoiceId,
    Guid ItemId,
    string? ReceiptNumber,
    Guid ConcurrencyStamp) : IRequest<Result<UpdateInvoiceItemResult>>;

public sealed record UpdateInvoiceItemResult(
    Guid Id,
    string? ReceiptNumber,
    Guid ConcurrencyStamp);

internal sealed class UpdateInvoiceItemCommandValidator
    : AbstractValidator<UpdateInvoiceItemCommand>
{
    public UpdateInvoiceItemCommandValidator()
    {
        RuleFor(command => command.InvoiceId).NotEmpty();
        RuleFor(command => command.ItemId).NotEmpty();
        RuleFor(command => command.ReceiptNumber).MaximumLength(100);
        RuleFor(command => command.ConcurrencyStamp).NotEmpty();
    }
}

internal sealed class UpdateInvoiceItemCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdateInvoiceItemCommand, Result<UpdateInvoiceItemResult>>
{
    public async Task<Result<UpdateInvoiceItemResult>> Handle(
        UpdateInvoiceItemCommand request,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginSerializableTransaction(cancellationToken);
        try
        {
            var result = await ApplyAsync(request, cancellationToken);
            if (result.IsSuccess) await unitOfWork.CommitTransaction(cancellationToken);
            else await unitOfWork.RollBackTransaction(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.RollBackTransaction(cancellationToken);
            throw;
        }
    }

    private async Task<Result<UpdateInvoiceItemResult>> ApplyAsync(
        UpdateInvoiceItemCommand request, CancellationToken cancellationToken)
    {
        var item = await unitOfWork.Repository<GoodsReceiptItem>()
            .GetByIdAsync(request.ItemId, cancellationToken);
        if (item is null ||
            item.InvoiceId != request.InvoiceId ||
            item.InvoiceStatus != InvoiceStatus.HasInvoice)
        {
            return Result<UpdateInvoiceItemResult>.Error("ردیف فاکتور پیدا نشد.");
        }

        var receipt = await unitOfWork.Repository<GoodsReceipt>()
            .GetByIdAsync(item.GoodsReceiptId, cancellationToken);
        if (receipt is null)
        {
            return Result<UpdateInvoiceItemResult>.Error("سند ورود مرتبط با ردیف فاکتور پیدا نشد.");
        }

        if (await InvoiceEditGuard.IsReceiptLockedAsync(unitOfWork, receipt.Id, cancellationToken))
            return Result<UpdateInvoiceItemResult>.Error(Invoice.LockedMessage);
        if (!receipt.TryAdvanceConcurrencyStamp(request.ConcurrencyStamp))
        {
            return Result<UpdateInvoiceItemResult>.Error(ConcurrencyMessages.Conflict);
        }

        item.UpdateReceiptNumber(request.ReceiptNumber);
        await unitOfWork.SaveAsync(cancellationToken);

        return Result<UpdateInvoiceItemResult>.Success(new(
            item.Id,
            item.ReceiptNumber,
            receipt.ConcurrencyStamp));
    }
}
