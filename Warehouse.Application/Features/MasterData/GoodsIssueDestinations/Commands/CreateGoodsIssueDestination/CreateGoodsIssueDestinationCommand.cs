using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Commands.CreateGoodsIssueDestination;

public sealed record CreateGoodsIssueDestinationCommand(string Title, string? Description)
    : IRequest<Result<TitledMasterDataSummary>>;

internal sealed class CreateGoodsIssueDestinationCommandValidator
    : AbstractValidator<CreateGoodsIssueDestinationCommand>
{
    public CreateGoodsIssueDestinationCommandValidator() =>
        MasterDataValidationRules.AddTitleRules(this, command => command.Title, command => command.Description);
}

internal sealed class CreateGoodsIssueDestinationCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<CreateGoodsIssueDestinationCommand, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        CreateGoodsIssueDestinationCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsIssueDestination>();
        if (await repository.CountAsync(repository.Entities.Where(item => item.Title == request.Title.Trim()), cancellationToken) > 0)
            return Result<TitledMasterDataSummary>.Error("این مقصد قبلاً ثبت شده است.");
        var item = new GoodsIssueDestination(request.Title, request.Description);
        await repository.AddAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result<TitledMasterDataSummary>.Success(ToSummary(item));
    }

    private static TitledMasterDataSummary ToSummary(GoodsIssueDestination item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description
    };
}
