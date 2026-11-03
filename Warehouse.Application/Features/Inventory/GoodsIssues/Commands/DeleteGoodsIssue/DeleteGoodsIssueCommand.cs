using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Commands.DeleteGoodsIssue;

public sealed record DeleteGoodsIssueCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteGoodsIssueCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<DeleteGoodsIssueCommand, Result>
{
    public async Task<Result> Handle(DeleteGoodsIssueCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsIssue>();
        var issue = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (issue is null) return Result.Error("سند خروج کالا پیدا نشد.");
        await unitOfWork.BeginTransaction(cancellationToken);
        try
        {
            var receipts = unitOfWork.Repository<GoodsReceiptItem>();
            var linked = await receipts.ToListAsync(receipts.Entities.Where(item =>
                item.GoodsIssueItem != null && item.GoodsIssueItem.GoodsIssueId == issue.Id), cancellationToken);
            foreach (var item in linked) item.SetGoodsIssueItem(null);
            await repository.DeleteAsync(issue, cancellationToken);
            await unitOfWork.SaveAsync(cancellationToken);
            await unitOfWork.CommitTransaction(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollBackTransaction(cancellationToken);
            throw;
        }
        return Result.Success();
    }
}
