using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.Invoices.Commands.DeleteInvoice;

public sealed record DeleteInvoiceCommand(Guid Id) : IRequest<Result<bool>>;

internal sealed class DeleteInvoiceCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<DeleteInvoiceCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteInvoiceCommand request, CancellationToken token)
    {
        await unitOfWork.BeginSerializableTransaction(token);
        try
        {
            var repository = unitOfWork.Repository<Invoice>();
            var invoice = await repository.GetByIdAsync(request.Id, token);
            if (invoice is null)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<bool>.Error("فاکتور پیدا نشد.");
            }
            if (invoice.IsLocked)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<bool>.Error("فاکتور ارسال‌شده یا تأییدشده در واحد مالی قابل حذف نیست.");
            }
            var items = unitOfWork.Repository<GoodsReceiptItem>();
            if (await items.CountAsync(items.Entities.Where(item => item.InvoiceId == request.Id), token) > 0)
            {
                await unitOfWork.RollBackTransaction(token);
                return Result<bool>.Error("فقط فاکتور بدون قلم کالا قابل حذف است.");
            }
            await repository.DeleteAsync(invoice, token);
            await unitOfWork.SaveAsync(token);
            await unitOfWork.CommitTransaction(token);
            return Result<bool>.Success(true);
        }
        catch
        {
            await unitOfWork.RollBackTransaction(token);
            throw;
        }
    }
}
