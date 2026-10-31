using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.LinkGoodsIssueItem;

public sealed record LinkGoodsIssueItemCommand(
    Guid GoodsReceiptItemId,
    Guid? GoodsIssueItemId,
    Guid ConcurrencyStamp) : IRequest<Result<LinkGoodsIssueItemResult>>;

public sealed record LinkGoodsIssueItemResult(
    Guid GoodsReceiptItemId,
    Guid? GoodsIssueItemId,
    Guid ConcurrencyStamp);

internal sealed class LinkGoodsIssueItemCommandValidator : AbstractValidator<LinkGoodsIssueItemCommand>
{
    public LinkGoodsIssueItemCommandValidator()
    {
        RuleFor(command => command.GoodsReceiptItemId).NotEmpty();
        RuleFor(command => command.GoodsIssueItemId).NotEmpty()
            .When(command => command.GoodsIssueItemId.HasValue);
        RuleFor(command => command.ConcurrencyStamp).NotEmpty();
    }
}

internal sealed class LinkGoodsIssueItemCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<LinkGoodsIssueItemCommand, Result<LinkGoodsIssueItemResult>>
{
    public async Task<Result<LinkGoodsIssueItemResult>> Handle(
        LinkGoodsIssueItemCommand request,
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

    private async Task<Result<LinkGoodsIssueItemResult>> ApplyAsync(
        LinkGoodsIssueItemCommand request, CancellationToken cancellationToken)
    {
        var receiptItem = await unitOfWork.Repository<GoodsReceiptItem>()
            .GetByIdAsync(request.GoodsReceiptItemId, cancellationToken);
        if (receiptItem is null)
            return Result<LinkGoodsIssueItemResult>.Error("آیتم ورود کالا پیدا نشد.");

        GoodsIssueItem? issueItem = null;
        if (request.GoodsIssueItemId is Guid issueItemId)
        {
            issueItem = await unitOfWork.Repository<GoodsIssueItem>()
                .GetByIdAsync(issueItemId, cancellationToken);
            if (issueItem is null)
                return Result<LinkGoodsIssueItemResult>.Error("آیتم خروج کالا پیدا نشد.");
        }

        var receipt = await unitOfWork.Repository<GoodsReceipt>()
            .GetByIdAsync(receiptItem.GoodsReceiptId, cancellationToken);
        if (receipt is null)
            return Result<LinkGoodsIssueItemResult>.Error("سند ورود کالا پیدا نشد.");
        if (await Invoices.InvoiceEditGuard.IsReceiptLockedAsync(unitOfWork, receipt.Id, cancellationToken))
            return Result<LinkGoodsIssueItemResult>.Error(Invoice.LockedMessage);
        if (!receipt.TryAdvanceConcurrencyStamp(request.ConcurrencyStamp))
            return Result<LinkGoodsIssueItemResult>.Error(ConcurrencyMessages.Conflict);

        receiptItem.SetGoodsIssueItem(issueItem);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result<LinkGoodsIssueItemResult>.Success(new(
            receiptItem.Id,
            receiptItem.GoodsIssueItemId,
            receipt.ConcurrencyStamp));
    }
}
