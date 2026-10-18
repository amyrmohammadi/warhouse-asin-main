using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Products.Commands.DeleteProduct;

internal sealed class DeleteProductCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<DeleteProductCommand, Result>
{
    public async Task<Result> Handle(
        DeleteProductCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Product>();
        var product = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
        {
            return Result.Error("کالای مورد نظر پیدا نشد.");
        }

        var receiptItems = unitOfWork.Repository<GoodsReceiptItem>();
        if (await receiptItems.CountAsync(
                receiptItems.Entities.Where(item => item.ProductId == request.Id),
                cancellationToken) > 0)
        {
            return Result.Error("این کالا در تاریخچه ورود کالا استفاده شده و قابل حذف نیست.");
        }

        await repository.DeleteAsync(product, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
