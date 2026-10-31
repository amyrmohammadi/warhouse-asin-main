using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.DeleteSupplier;

internal sealed class DeleteSupplierCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<DeleteSupplierCommand, Result>
{
    public async Task<Result> Handle(
        DeleteSupplierCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Supplier>();
        var item = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result.Error("The requested supplier was not found.");
        }

        var invoices = unitOfWork.Repository<Invoice>();
        if (await invoices.CountAsync(
                invoices.Entities.Where(invoice => invoice.SupplierId == request.Id),
                cancellationToken) > 0)
        {
            return Result.Error(
                "این تأمین‌کننده در تاریخچه ورود کالا استفاده شده و قابل حذف نیست.");
        }

        var receiptItems = unitOfWork.Repository<GoodsReceiptItem>();
        if (await receiptItems.CountAsync(
                receiptItems.Entities.Where(receiptItem => receiptItem.SupplierId == request.Id),
                cancellationToken) > 0)
        {
            return Result.Error(
                "این تأمین‌کننده در تاریخچه ورود کالا استفاده شده و قابل حذف نیست.");
        }

        await repository.DeleteAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
