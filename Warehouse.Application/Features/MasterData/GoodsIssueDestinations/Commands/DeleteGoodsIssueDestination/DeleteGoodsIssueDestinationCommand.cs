using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Commands.DeleteGoodsIssueDestination;

public sealed record DeleteGoodsIssueDestinationCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteGoodsIssueDestinationCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<DeleteGoodsIssueDestinationCommand, Result>
{
    public async Task<Result> Handle(
        DeleteGoodsIssueDestinationCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsIssueDestination>();
        var item = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null) return Result.Error("مقصد پیدا نشد.");
        if (await repository.CountAsync(repository.Entities.Where(value =>
                value.Id == request.Id && value.GoodsIssueItems.Any()), cancellationToken) > 0)
            return Result.Error("این مقصد در خروج کالا استفاده شده و قابل حذف نیست.");
        await repository.DeleteAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
