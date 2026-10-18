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

        await repository.DeleteAsync(receipt, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
