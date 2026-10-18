using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptById;

internal sealed class GetGoodsReceiptByIdQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsReceiptByIdQuery, Result<GoodsReceiptSummary>>
{
    public async Task<Result<GoodsReceiptSummary>> Handle(
        GetGoodsReceiptByIdQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsReceipt>();
        var items = await repository.ToListAsync(
            repository.Entities.Where(item => item.Id == request.Id).ToSummary(),
            cancellationToken);
        return items.Count == 0
            ? Result<GoodsReceiptSummary>.Error("سند ورود کالا پیدا نشد.")
            : Result<GoodsReceiptSummary>.Success(items[0]);
    }
}
