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
        var itemRepository = unitOfWork.Repository<GoodsIssueItem>();
        var linkedItems = await itemRepository.CountAsync(
            itemRepository.Entities.Where(item =>
                item.GoodsIssueId == issue.Id && item.GoodsReceiptItems.Any()),
            cancellationToken);
        if (linkedItems > 0)
            return Result.Error("ابتدا ارتباط آیتم‌های ورود کالا با این سند خروج را حذف کنید.");
        await repository.DeleteAsync(issue, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
