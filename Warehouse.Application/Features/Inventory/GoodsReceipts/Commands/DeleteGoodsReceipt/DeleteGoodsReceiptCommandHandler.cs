using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.DeleteGoodsReceipt;

internal sealed class DeleteGoodsReceiptCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<DeleteGoodsReceiptCommand, Result>
{
    public async Task<Result> Handle(
        DeleteGoodsReceiptCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsReceipt>();
        var receipt = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (receipt is null)
        {
            return Result.Error("سند ورود کالا پیدا نشد.");
        }

        await unitOfWork.BeginSerializableTransaction(cancellationToken);
        try
        {
            var itemRepository = unitOfWork.Repository<GoodsReceiptItem>();
            var linkedItems = await itemRepository.ToListAsync(itemRepository.Entities
                .Where(item => item.GoodsReceiptId == receipt.Id && item.GoodsIssueItemId.HasValue),
                cancellationToken);
            foreach (var issueId in linkedItems.Select(item => item.GoodsIssueItemId!.Value).Distinct())
            {
                var remaining = await itemRepository.ToListAsync(itemRepository.Entities
                    .Where(item => item.GoodsIssueItemId == issueId && item.GoodsReceiptId != receipt.Id)
                    .Select(item => item.GoodsReceipt.EnteredAtUtc), cancellationToken);
                var issueItem = await unitOfWork.Repository<GoodsIssueItem>()
                    .GetByIdAsync(issueId, cancellationToken);
                issueItem?.SetReturnAtUtc(remaining.Count > 0 ? remaining.Max() : null);
            }
            await repository.DeleteAsync(receipt, cancellationToken);
            await unitOfWork.SaveAsync(cancellationToken);
            await unitOfWork.CommitTransaction(cancellationToken);
            return Result.Success();
        }
        catch
        {
            await unitOfWork.RollBackTransaction(cancellationToken);
            throw;
        }
    }
}
