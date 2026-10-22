using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Commands.UpdateGoodsIssueDestination;

public sealed record UpdateGoodsIssueDestinationCommand(Guid Id, string Title, string? Description)
    : IRequest<Result<TitledMasterDataSummary>>;

internal sealed class UpdateGoodsIssueDestinationCommandValidator
    : AbstractValidator<UpdateGoodsIssueDestinationCommand>
{
    public UpdateGoodsIssueDestinationCommandValidator() =>
        MasterDataValidationRules.AddTitleRules(this, command => command.Title, command => command.Description);
}

internal sealed class UpdateGoodsIssueDestinationCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdateGoodsIssueDestinationCommand, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        UpdateGoodsIssueDestinationCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsIssueDestination>();
        var item = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null) return Result<TitledMasterDataSummary>.Error("مقصد پیدا نشد.");
        if (await repository.CountAsync(repository.Entities.Where(value =>
                value.Id != request.Id && value.Title == request.Title.Trim()), cancellationToken) > 0)
            return Result<TitledMasterDataSummary>.Error("این مقصد قبلاً ثبت شده است.");
        item.Update(request.Title, request.Description);
        await repository.UpdateAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result<TitledMasterDataSummary>.Success(new TitledMasterDataSummary
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description
        });
    }
}
