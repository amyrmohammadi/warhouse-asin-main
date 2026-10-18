using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.People.Commands.DeletePerson;

internal sealed class DeletePersonCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<DeletePersonCommand, Result>
{
    public async Task<Result> Handle(
        DeletePersonCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Person>();
        var person = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (person is null)
        {
            return Result.Error("The requested person was not found.");
        }

        var receipts = unitOfWork.Repository<GoodsReceipt>();
        if (await receipts.CountAsync(
                receipts.Entities.Where(receipt => receipt.DelivererId == request.Id),
                cancellationToken) > 0)
        {
            return Result.Error("این شخص به‌عنوان تحویل‌دهنده در تاریخچه ورود کالا استفاده شده و قابل حذف نیست.");
        }

        await repository.DeleteAsync(person, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
